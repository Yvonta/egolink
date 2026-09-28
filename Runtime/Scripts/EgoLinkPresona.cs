using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Yvonta
{
    // --- Data Transfer Objects for the 'presona' method ---
    [Serializable]
    public class InvarsData
    {
        public string location;
        public string clothing;
        public string datetime;
        public string conversationpartner;

        public InvarsData(string location, string clothing, string datetime, string conversationpartner)
        {
            this.location = location;
            this.clothing = clothing;
            this.datetime = datetime;
            this.conversationpartner = conversationpartner;
        }
    }

    [Serializable]
    public class PersonaParams
    {
        public string name;
        public InvarsData invars;

        public PersonaParams(string name, InvarsData invars)
        {
            this.name = name;
            this.invars = invars;
        }
    }

    [Serializable]
    public class PersonaData
    {
        public string owner;
        public string name;
        public string role;
        public string voice;
    }

    [Serializable]
    public class PersonaResult
    {
        public int code;
        public string message;
        public PersonaData data;
    }

    // --- Persona API Service ---

    public class EgoLinkPersona
    {
        private readonly EgoLinkJsonRpcClient _rpcClient;

        public EgoLinkPersona(EgoLinkJsonRpcClient rpcClient)
        {
            _rpcClient = rpcClient;
        }

        /// <summary>
        /// Calls the PHP backend 'presona' JSON-RPC method.
        /// </summary>
        /// <param name="id">The persona/clone ID to request.</param>
        /// <param name="overrideVars">Dictionary containing key-value pairs to override prompt variables.</param>
        /// <returns>PersonaResult containing code, message, and persona data on success.</returns>
        public async Task<PersonaResult> GetPersonaAsync(string name, string location, string chlothing, string converstaionpartner)
        {


            DateTime now = DateTime.Now;
            string datetime = now.ToLongDateString() + " " + now.ToShortTimeString();
            var parameters = new PersonaParams(name, new InvarsData(location, chlothing, datetime, converstaionpartner));

            try
            {
                PersonaResult result = await _rpcClient.SendRequestAsync<PersonaParams, PersonaResult>("presona", parameters);
                return result;
            }
            catch (JsonRpcException ex)
            {
                Debug.LogError($"JSON-RPC Error getting Persona: Code {ex.Code} - {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to fetch persona: {ex.Message}");
                throw;
            }
        }
    }
}