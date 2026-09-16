using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Donkey
{
    // --- Data Transfer Objects (Generic) ---

    [Serializable]
    public class JsonRpcRequest<TParams>
    {
        public string jsonrpc = "2.0";
        public string method;
        public TParams @params;
        public int id;

        public JsonRpcRequest(string method, TParams @params, int id = 1)
        {
            this.method = method;
            this.@params = @params;
            this.id = id;
        }
    }

    [Serializable]
    public class JsonRpcResponse<TResult>
    {
        public string jsonrpc;
        public TResult result;
        public JsonRpcError error;
        public int id;
    }

    [Serializable]
    public class JsonRpcError
    {
        public int code;
        public string message;
        public string data;
    }

    public class JsonRpcException : Exception
    {
        public int Code { get; }
        
        public JsonRpcException(int code, string message) : base($"JSON-RPC Error [{code}]: {message}")
        {
            Code = code;
        }
    }

    // --- Core Client Service ---

    public class DonkeyJsonRpcClient
    {
        private readonly string _endpointUrl;
        private readonly int _timeoutSeconds;
        private int _requestIdCounter = 1;

        public DonkeyJsonRpcClient(string endpointUrl, int timeoutSeconds = 10)
        {
            _endpointUrl = endpointUrl;
            _timeoutSeconds = timeoutSeconds;
        }

        /// <summary>
        /// Sends a JSON-RPC request and returns the strongly-typed result.
        /// </summary>
        public async Task<TResult> SendRequestAsync<TParams, TResult>(string method, TParams parameters)
        {
            int currentId = _requestIdCounter++;
            var requestData = new JsonRpcRequest<TParams>(method, parameters, currentId);
            string jsonPayload = JsonUtility.ToJson(requestData);

            using (var webRequest = new UnityWebRequest(_endpointUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.timeout = _timeoutSeconds;

                // Send request asynchronously using Unity's async operation awaiter
                var operation = webRequest.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                // Handle HTTP / Network level errors
                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"Network Error: {webRequest.error} | Response: {webRequest.downloadHandler.text}");
                }

                // Deserialize JSON-RPC Response wrapper
                string jsonResponse = webRequest.downloadHandler.text;
                var response = JsonUtility.FromJson<JsonRpcResponse<TResult>>(jsonResponse);

                // Handle JSON-RPC level errors returned by the server
                if (response.error != null && response.error.code != 0)
                {
                    throw new JsonRpcException(response.error.code, response.error.message);
                }

                return response.result;
            }
        }
    }
}