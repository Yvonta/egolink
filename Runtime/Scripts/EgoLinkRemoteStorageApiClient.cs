using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Yvonta
{
    public class EgoLinkRemoteStorageApiClient
    {
        private readonly EgoLinkJsonRpcClient _rpcClient;

        public EgoLinkRemoteStorageApiClient(EgoLinkJsonRpcClient rpcClient)
        {
            _rpcClient = rpcClient;
        }

        #region Data Models & RPC Payloads

        [Serializable]
        public class ServerResult
        {
            public int code;
            public string message;
            public DataItem data;
        }

        [Serializable]
        public class CategoryItem
        {
            public int id;
            public string name;
            public int parrent_id;
            public int weight;
        }

        [Serializable]
        public class DataItem
        {
            public int id;
            public string name;
            public int cat_id;
            public int weight;
            public int user_id;
            public int group_id;
            public int permission;
            public string created;
            public string changed;
            public string expire;
            public string type;
            public string subtype;
            public string value;
        }

        // Positional parameter wrappers for Unity's JsonUtility serialization

        [Serializable]
        private class GetCatsParams
        {
            public string name;
            public GetCatsParams(string name) => this.name = name;
        }

        [Serializable]
        private class PutCatParams
        {
            public string name;
            public PutCatParams(string name)
            {
                this.name = name;
            }
        }

        [Serializable]
        private class PutDataParams
        {
            public string name;
            public string catname;
            public string subtype;
            public string data;
            public int length;

            public PutDataParams(string name, string catname, string subtype, string data, int length)
            {
                this.name = name;
                this.catname = catname;
                this.subtype = subtype;
                this.data = data;
                this.length = length;
            }
        }

        [Serializable]
        private class GetDataParams
        {
            public string name;
            public string catname;

            public GetDataParams(string name, string catname)
            {
                this.name = name;
                this.catname = catname;
            }
        }

        #endregion

        #region API Methods

        /// <summary>
        /// Calls PHP method getcats($catid)
        /// </summary>
        public async Task<ServerResult> GetCatsAsync(string name)
        {
            var paramsObj = new GetCatsParams(name);
            return await _rpcClient.SendRequestAsync<GetCatsParams, ServerResult>("getcats", paramsObj);
        }

        /// <summary>
        /// Calls PHP method putcat($name, $catid)
        /// </summary>
        public async Task<ServerResult> PutCatAsync(string name)
        {
            var paramsObj = new PutCatParams(name);
            return await _rpcClient.SendRequestAsync<PutCatParams, ServerResult>("putcat", paramsObj);
        }

        /// <summary>
        /// Calls PHP method putdata($name, $catname, $subtype, $data, $length)
        /// </summary>
        public async Task<ServerResult> PutDataAsync(string name, string catName, string subType, string data)
        {
            int length = Encoding.UTF8.GetByteCount(data ?? string.Empty);
            var paramsObj = new PutDataParams(name, catName, subType, data, length);
            return await _rpcClient.SendRequestAsync<PutDataParams, ServerResult>("putdata", paramsObj);
        }
        public async Task<ServerResult> PutDataAsync(string name, string catName, string subType, byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return null;
            }

            // Safely encode raw binary GLB data to a Base64 string
            string sdata = Convert.ToBase64String(data);
            int length = data.Length;

            var paramsObj = new PutDataParams(name, catName, subType, sdata, length);
            return await _rpcClient.SendRequestAsync<PutDataParams, ServerResult>("putdata", paramsObj);
        }

        /// <summary>
        /// Calls PHP method getdata($name, $catname)
        /// </summary>
        public async Task<ServerResult> GetDataAsync(string name, string catName)
        {
            var paramsObj = new GetDataParams(name, catName);
            return await _rpcClient.SendRequestAsync<GetDataParams, ServerResult>("getdata", paramsObj);
        }

        #endregion
    }
}