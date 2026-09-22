using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace Yvonta
{
    [Serializable]
    public class ChatMessage
    {
        public string role; // "system", "user", or "assistant"
        public string content;

        public ChatMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }

    public class EgoLinkLLMStreaming : MonoBehaviour
    {
        public delegate void SentenceReceivedHandler(string sentence);

        [SerializeField] private string defaultApiUrl = "https://yvonta.net/appapi/v2/llm.php";
        [SerializeField] private string modelName = "gemma2:2b";
        
        [TextArea(3, 5)]
        [SerializeField] private string systemPrompt = "You are a helpful assistant.";

        private StringBuilder buffer = new StringBuilder();
        private StringBuilder currentAssistantResponse = new StringBuilder();
        private EgoLinkSession session;

        // Stores conversation history
        private List<ChatMessage> conversationHistory = new List<ChatMessage>();

        public void Initialize(EgoLinkSession activeSession)
        {
            this.session = activeSession;
            Debug.Log($"[EgoLinkLLMStreaming] Initialized with session instance: {(session != null ? "Valid" : "Null")}");
        }

        /// <summary>
        /// Update or change the system prompt dynamically.
        /// </summary>
        public void SetSystemPrompt(string newSystemPrompt)
        {
            systemPrompt = newSystemPrompt;
        }

        /// <summary>
        /// Clear conversation history if starting a new session.
        /// </summary>
        public void ClearHistory()
        {
            conversationHistory.Clear();
        }

        public void RequestStream(string userPrompt, SentenceReceivedHandler onSentenceReady)
        {
            RequestStream(userPrompt, onSentenceReady, defaultApiUrl);
        }

        public void RequestStream(string userPrompt, SentenceReceivedHandler onSentenceReady, string overrideUrl)
        {
            string targetUrl = !string.IsNullOrEmpty(overrideUrl) ? overrideUrl : defaultApiUrl;
            StartCoroutine(StreamRoutine(userPrompt, targetUrl, onSentenceReady));
        }

        private IEnumerator StreamRoutine(string userPrompt, string targetUrl, SentenceReceivedHandler onSentenceReady)
        {
            buffer.Clear();
            currentAssistantResponse.Clear();

            // Construct payload with System Prompt and History using Chat API format
            List<ChatMessage> fullMessages = new List<ChatMessage>();

            if (!string.IsNullOrEmpty(systemPrompt))
            {
                fullMessages.Add(new ChatMessage("system", systemPrompt));
            }

            fullMessages.AddRange(conversationHistory);
            fullMessages.Add(new ChatMessage("user", userPrompt));

            string jsonPayload = BuildChatJsonPayload(modelName, fullMessages);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);

            using (UnityWebRequest request = new UnityWebRequest(targetUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(payloadBytes);
                request.uploadHandler.contentType = "application/json";

                string sessionId = session != null ? session.StoredCookie : null;
                if (!string.IsNullOrEmpty(sessionId))
                {
                    string cookieHeader = sessionId.StartsWith("SESSION=") ? sessionId : $"SESSION={sessionId}";
                    request.SetRequestHeader("Cookie", cookieHeader);
                }

                SentenceDownloadHandler streamHandler = new SentenceDownloadHandler(onSentenceReady, buffer, currentAssistantResponse);
                request.downloadHandler = streamHandler;

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[EgoLinkLLMStreaming] Error: {request.error} | Code: {request.responseCode}");
                }
                else
                {
                    streamHandler.FlushRemaining(onSentenceReady);

                    // Add both user input and full AI response into conversation history after success
                    conversationHistory.Add(new ChatMessage("user", userPrompt));
                    conversationHistory.Add(new ChatMessage("assistant", currentAssistantResponse.ToString()));
                    Debug.Log($"[EgoLinkLLMStreaming] Successfully added response to history. Total messages: {conversationHistory.Count}");
                }
            }
        }

        private string BuildChatJsonPayload(string model, List<ChatMessage> messages)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"model\":\"{EscapeJson(model)}\",");
            sb.Append("\"stream\":true,");
            sb.Append("\"messages\":[");

            for (int i = 0; i < messages.Count; i++)
            {
                sb.Append("{");
                sb.Append($"\"role\":\"{EscapeJson(messages[i].role)}\",");
                sb.Append($"\"content\":\"{EscapeJson(messages[i].content)}\"");
                sb.Append("}");
                if (i < messages.Count - 1) sb.Append(",");
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private static string EscapeJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("\\", "\\\\")
                       .Replace("\"", "\\\"")
                       .Replace("\n", "\\n")
                       .Replace("\r", "\\r")
                       .Replace("\t", "\\t");
        }

        private class SentenceDownloadHandler : DownloadHandlerScript
        {
            private readonly SentenceReceivedHandler sentenceCallback;
            private readonly StringBuilder textBuffer;
            private readonly StringBuilder fullResponseTracker;
            private StringBuilder rawChunkBuffer = new StringBuilder();

            // Compatible with both standard Ollama Chat API (`"content":"..."`) and standard API outputs
            private static readonly Regex ResponseRegex = new Regex(@"\""(?:content|response)\""\s*:\s*\""(.*?)\""", RegexOptions.Compiled);
            private static readonly Regex SentenceBoundaryRegex = new Regex(@"(?<=[.!?])\s+|(\r?\n){2,}|\r?\n", RegexOptions.Compiled);

            public SentenceDownloadHandler(SentenceReceivedHandler callback, StringBuilder buffer, StringBuilder fullResponseTracker) : base(new byte[4096])
            {
                this.sentenceCallback = callback;
                this.textBuffer = buffer;
                this.fullResponseTracker = fullResponseTracker;
            }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength < 1) return true;

                string chunk = Encoding.UTF8.GetString(data, 0, dataLength);
                rawChunkBuffer.Append(chunk);

                string rawText = rawChunkBuffer.ToString();
                string[] lines = rawText.Split('\n');

                rawChunkBuffer.Clear();
                rawChunkBuffer.Append(lines[lines.Length - 1]);

                for (int i = 0; i < lines.Length - 1; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    Match match = ResponseRegex.Match(line);
                    if (match.Success)
                    {
                        string extractedContent = UnescapeJsonString(match.Groups[1].Value);
                        textBuffer.Append(extractedContent);
                        fullResponseTracker.Append(extractedContent);
                    }
                }

                ProcessSentences();
                return true;
            }

            private void ProcessSentences()
            {
                string currentText = textBuffer.ToString();
                MatchCollection matches = SentenceBoundaryRegex.Matches(currentText);

                if (matches.Count > 0)
                {
                    int lastCutIndex = 0;

                    foreach (Match match in matches)
                    {
                        int length = match.Index - lastCutIndex;
                        string sentence = currentText.Substring(lastCutIndex, length).Trim();

                        if (!string.IsNullOrEmpty(sentence))
                        {
                            sentenceCallback?.Invoke(sentence);
                        }

                        if (Regex.IsMatch(match.Value, @"(\r?\n){2,}"))
                        {
                            sentenceCallback?.Invoke("[PARAGRAPH_BREAK]");
                        }

                        lastCutIndex = match.Index + match.Length;
                    }

                    textBuffer.Clear();
                    textBuffer.Append(currentText.Substring(lastCutIndex));
                }
            }

            public void FlushRemaining(SentenceReceivedHandler callback)
            {
                if (rawChunkBuffer.Length > 0)
                {
                    Match match = ResponseRegex.Match(rawChunkBuffer.ToString());
                    if (match.Success)
                    {
                        string extracted = UnescapeJsonString(match.Groups[1].Value);
                        textBuffer.Append(extracted);
                        fullResponseTracker.Append(extracted);
                    }
                }

                string remaining = textBuffer.ToString().Trim();
                if (!string.IsNullOrEmpty(remaining))
                {
                    callback?.Invoke(remaining);
                    textBuffer.Clear();
                }
            }

            private string UnescapeJsonString(string text)
            {
                return text.Replace("\\\"", "\"")
                           .Replace("\\\\", "\\")
                           .Replace("\\n", "\n")
                           .Replace("\\r", "\r")
                           .Replace("\\t", "\t");
            }
        }
    }
}