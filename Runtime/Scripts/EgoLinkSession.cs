using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Yvonta
{
    // --- Request Parameter DTOs ---

    [Serializable]
    public class EgoLinkLoginParams
    {
        public string email;
        public string password;
    }

    [Serializable]
    public class EgoLinkRegisterParams
    {
        public string email;
        public string password;
        public string name;
        public string gender;
        public string birthdate;
    }

    [Serializable]
    public class EmptyParams { }

    // --- Response Data DTOs ---

    [Serializable]
    public class LoginResultData
    {
        public int userid;
        public string email;
        public string sessionid;
        public string role;
        public string name;
    }

    [Serializable]
    public class LoginResult
    {
        public int code;
        public string message;
        public LoginResultData data;
    }

    [Serializable]
    public class LoggedInResultData
    {
        public int code;
        public string message;
    }

    [Serializable]
    public class UserStatsResultData
    {
        public int userstotal;
        public int usersonline;
        public int usersplaying;
    }

    [Serializable]
    public class UserStatsResult
    {
        public int code;
        public string message;
        public UserStatsResultData data;
    }
    
    [Serializable]
    public class LogoutResultData
    {
        public int code;
        public string message;
    }

    // --- Refactored Session Service ---

    public class EgoLinkSession
    {
        private readonly EgoLinkJsonRpcClient _rpcClient;
        private string _storedSessionId;

        public string StoredCookie => _storedSessionId;

        public EgoLinkSession(string serverUrl)
        {
            _rpcClient = new EgoLinkJsonRpcClient(serverUrl);
            LoadSessionFromDisk();
        }

        public async Task<UserStatsResult> UserStatsAsync()
        {
            UserStatsResult result = await _rpcClient.SendRequestAsync<EmptyParams, UserStatsResult>("userstats", new EmptyParams());
            return result;
        }

        public async Task<LoginResult> LoginAsync(string email, string password)
        {
            var parameters = new EgoLinkLoginParams
            {
                email = email,
                password = password
            };

            LoginResult result = await _rpcClient.SendRequestAsync<EgoLinkLoginParams, LoginResult>("login", parameters);

            if (result?.data?.sessionid != null)
            {
                _storedSessionId = result.data.sessionid;
                EgoLinkSessionSave.SaveSession(_storedSessionId);
            }

            return result;
        }

        public async Task<bool> LogoutAsync()
        {
            LogoutResultData result = await _rpcClient.SendRequestAsync<EmptyParams, LogoutResultData>("logout", new EmptyParams());
            return result != null && result.code == 0;
        }

        public async Task<bool> IsLoggedInAsync()
        {
            LoggedInResultData result = await _rpcClient.SendRequestAsync<EmptyParams, LoggedInResultData>("isloggedin", new EmptyParams());
            return result != null && result.code == 0;
        }

        public async Task<LoginResult> RegisterAsync(string email, string password, string name, string gender, string birthdate)
        {
            var parameters = new EgoLinkRegisterParams
            {
                email = email,
                password = password,
                name = name,
                gender = gender,
                birthdate = birthdate
            };

            LoginResult result = await _rpcClient.SendRequestAsync<EgoLinkRegisterParams, LoginResult>("register", parameters);

            if (result?.data?.sessionid != null)
            {
                _storedSessionId = result.data.sessionid;
                EgoLinkSessionSave.SaveSession(_storedSessionId);
            }

            return result;
        }

        public void ClearSession()
        {
            _storedSessionId = null;
            EgoLinkSessionSave.ClearSession();
            Debug.Log("[EgoLinkSession] Session cleared.");
        }

        private void LoadSessionFromDisk()
        {
            _storedSessionId = EgoLinkSessionSave.LoadSession();
            if (_storedSessionId != null)
            {
                Debug.Log($"[EgoLinkSession] Loaded Session ID from Disk: {_storedSessionId}");
            }
            else
            {
                Debug.Log("[EgoLinkSession] New session!");
            }
        }
    }
}