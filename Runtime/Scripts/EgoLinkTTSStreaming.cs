using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Yvonta
{
    [RequireComponent(typeof(EgoLinkTTS))]
    public class EgoLinkTTSStreaming : MonoBehaviour
    {
        public const string PARAGRAPH_BREAK_TOKEN = "[PARAGRAPH_BREAK]";

        [SerializeField] private float pauseBetweenSentences = 0.2f;
        [SerializeField] private float pauseBetweenParagraphs = 0.6f;

        private EgoLinkTTS EgoLinkTTS;

        private struct SentenceItem
        {
            public string Text;
            public string Language;
        }

        private Queue<SentenceItem> sentenceQueue = new Queue<SentenceItem>();
        private bool isProcessingQueue = false;
        private EgoLinkLanguageDetector languageDetector;

        private void Start()
        {
            languageDetector = new EgoLinkLanguageDetector();
        }

        private void Awake()
        {
            EgoLinkTTS = GetComponent<EgoLinkTTS>();
            if (EgoLinkTTS == null)
            {
                EgoLinkTTS = gameObject.AddComponent<EgoLinkTTS>();
            }
        }

        public void SetVoice(string voice)
        {
            if (EgoLinkTTS != null)
            {
                EgoLinkTTS.SetVoice(voice);
            }
        }

        public void Initialize(EgoLinkSession session, float pauseBetweenSentences = 0.2f, float pauseBetweenParagraphs = 0.6f)
        {
            this.pauseBetweenSentences = pauseBetweenSentences;
            this.pauseBetweenParagraphs = pauseBetweenParagraphs;
            EgoLinkTTS.Initialize(session);
        }

        public void AddSentence(string sentence)
        {
            if (string.IsNullOrWhiteSpace(sentence)) return;

            string detectedLanguage = languageDetector.DetectLanguage(sentence);  

            if (sentence == PARAGRAPH_BREAK_TOKEN)
            {
                sentenceQueue.Enqueue(new SentenceItem { Text = PARAGRAPH_BREAK_TOKEN, Language = string.Empty });
            }
            else
            {
                string clean = EgoLinkStringHelper.RemoveEmojis(sentence);
                clean = EgoLinkStringHelper.RemoveNonVisibleChars(clean);
                clean = EgoLinkStringHelper.FilterAsterisks(clean);
                clean = EgoLinkStringHelper.FilterNewline(clean);

                if (string.IsNullOrWhiteSpace(clean)) return;

                clean = clean.Trim();
                sentenceQueue.Enqueue(new SentenceItem { Text = clean, Language = detectedLanguage });
            }

            if (!isProcessingQueue)
            {
                StartCoroutine(ProcessSentenceQueue());
            }
        }

        private IEnumerator ProcessSentenceQueue()
        {
            isProcessingQueue = true;

            // Start een achtergrond-coroutine die continu zinnen klaarzet in de buffer (max 2)
            Coroutine fetchCoroutine = StartCoroutine(BufferSentencesRoutine());

            while (sentenceQueue.Count > 0 || EgoLinkTTS.GetBufferedClipCount() > 0 || EgoLinkTTS.IsDownloading)
            {
                // Wacht tot er minimaal 1 audio clip klaar is om af te spelen
                yield return new WaitUntil(() => EgoLinkTTS.GetBufferedClipCount() > 0 || (!EgoLinkTTS.IsDownloading && sentenceQueue.Count == 0));

                if (EgoLinkTTS.GetBufferedClipCount() > 0)
                {
                    // Speel de voorste clip af
                    float clipLength = EgoLinkTTS.PlayNextClip();

                    // Wacht tot de clip klaar is met afspelen
                    yield return new WaitForSeconds(clipLength);

                    if (pauseBetweenSentences > 0f)
                    {
                        yield return new WaitForSeconds(pauseBetweenSentences);
                    }
                }
            }

            if (fetchCoroutine != null) StopCoroutine(fetchCoroutine);
            isProcessingQueue = false;
        }

        // Deze coroutine vult de buffer achter elkaar aan tot maximaal 2 zinnen
        private IEnumerator BufferSentencesRoutine()
        {
            const int MAX_BUFFER_SIZE = 3;

            while (true)
            {
                // Vult alleen aan als de buffer minder dan 2 zinnen heeft, 
                // er zinnen klaarstaan EN er niet al een download bezig is
                if (EgoLinkTTS.GetBufferedClipCount() < MAX_BUFFER_SIZE && sentenceQueue.Count > 0 && !EgoLinkTTS.IsDownloading)
                {
                    SentenceItem nextItem = sentenceQueue.Dequeue();

                    if (nextItem.Text == PARAGRAPH_BREAK_TOKEN)
                    {
                        if (pauseBetweenParagraphs > 0f)
                        {
                            yield return new WaitForSeconds(pauseBetweenParagraphs);
                        }
                        continue;
                    }

                    // Start de download voor deze zin
                    EgoLinkTTS.EnqueueParagraph(nextItem.Text, nextItem.Language);

                    // Wacht verplicht tot deze specifieke download VOLLEDIG is afgerond
                    // Dit zorgt ervoor dat downloads nooit tegelijk/parallel lopen
                    yield return new WaitUntil(() => !EgoLinkTTS.IsDownloading);
                }

                yield return null;
            }
        }

        public void StopStream()
        {
            StopAllCoroutines();
            sentenceQueue.Clear();
            if (EgoLinkTTS != null) EgoLinkTTS.ClearAll();
            isProcessingQueue = false;
        }
    }
}