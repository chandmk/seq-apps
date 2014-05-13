using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.Ontime
{
    [SeqApp("OnTime Incident",
        Description = "Posts seq event an incident in ontime")]
    public class OntimeTicketReactor : Reactor, ISubscribeTo<LogEventData>
    {
        /// <summary>
        ///  Seq Server Address
        /// </summary>
        /// <value>
        ///     Seq server address
        /// </value>
        [SeqAppSetting(
            DisplayName = "Seq Server Address",
            HelpText = "URL of the seq server. This appears in notes field so that you can get back to the event from ontime.")]
        public string SeqUrl { get; set; }

        /// <summary>
        ///     Gets the host.
        /// </summary>
        /// <value>
        ///     The host.
        /// </value>
        [SeqAppSetting(
            DisplayName = "OnTime Host (url)",
            HelpText = "URL of OnTime (do not include /api/ at the end of the path).")]
        public string Host { get; set; }

        /// <summary>
        ///     Gets the name of the project.
        /// </summary>
        /// <value>
        ///     The name of the project.
        /// </value>
        [SeqAppSetting(
            DisplayName = "Project Id",
            IsOptional = false,
            HelpText = "Project Id to post OnTime incident.")]
        public int ProjectId { get; set; }
       
        /// <summary>
        ///     Gets the username.
        /// </summary>
        /// <value>
        ///     The username.
        /// </value>
        [SeqAppSetting(
            DisplayName = "Username",
            IsOptional = false,
            HelpText = "Authenticated username for OnTime.")]
        public string Username { get; set; }

        /// <summary>
        ///     Gets the password.
        /// </summary>
        /// <value>
        ///     The password.
        /// </value>
        [SeqAppSetting(
            DisplayName = "Password",
            IsOptional = false,
            HelpText = "Authenticated username for OnTime.",
            InputType = SettingInputType.Password)]
        public string Password { get; set; } 
        
        /// <summary>
        ///     Gets the username.
        /// </summary>
        /// <value>
        ///     The username.
        /// </value>
        [SeqAppSetting(
            DisplayName = "Client Id",
            IsOptional = false,
            HelpText = "ClientId for OnTimeAPI.")]
        public string ClientId { get; set; }

        /// <summary>
        ///     Gets the password.
        /// </summary>
        /// <value>
        ///     The password.
        /// </value>
        [SeqAppSetting(
            DisplayName = "Client Secret",
            IsOptional = false,
            HelpText = "Client Secret for OnTimeAPI.",
            InputType = SettingInputType.Password)]
        public string ClientIdSecret { get; set; }

        public void On(Event<LogEventData> evt)
        {
            try
            {
                AuthorizedUser = FetchAccessToken();
                PostIncident(evt);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while creating incident in Ontime");
            }
        }

        AuthResponse AuthorizedUser { get; set; }

        private void PostIncident(Event<LogEventData> evt)
        {
            var message = evt.Data.Exception ?? evt.Data.RenderedMessage;
            var messageId = ComputeId(message);

            if (IncidentAlreadyExistsInOntime(messageId))
            {
                return;
            }
            var subject = messageId + " - " + evt.Data.RenderedMessage;
            var body = string.Format("{0} - {1} Exception Event Id #{2}\r\nException:\r\n{3}",
                evt.TimestampUtc.ToLocalTime(), evt.Data.Level, evt.Id, evt.Data.Exception);
            var surl = SeqUrl + "/#/now?filter=@Id%20%3D%3D%20%22" + evt.Id + "%22";
            var notes = string.Format("<a href='{0}'>{0}</a>", surl); 
            
            var incident = new Incident
            {
                Name = subject,
                Description = body,
                Project = new Project {Id = ProjectId},
                Assigned_To = new User {Id = AuthorizedUser.Data.Id},
                Notes = notes,
                Priority = Priotity.FromDebugLevel(evt.Data.Level),
            };
            var onTimeIncident = new OnTimeIncident
            {
                Item = incident
            };
            var parameters = new Dictionary<string, object> {
				 {"access_token", AuthorizedUser.Access_Token},
			};

            var url = GetUrl("/api/v2/incidents", parameters);
            using (var client = new HttpClient())
            {
                 var result = client.PostAsJsonAsync(url, onTimeIncident).Result;
                result.EnsureSuccessStatusCode();
            }
        }

        private static string ComputeId(string input)
        {
            MD5 md5 = MD5.Create();
            return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(input))).Replace("-", string.Empty);
        }

        private bool IncidentAlreadyExistsInOntime(string id)
        {
            var parameters = new Dictionary<string, object> {
				{ "project_id", ProjectId },
				{ "search_field", "name" },
				{"search_string", id},
				{"columns", "id"},
                {"access_token", AuthorizedUser.Access_Token},
			};
            var url = GetUrl("/api/v2/incidents", parameters);
            using (var client = new HttpClient())
            {
                var response = client.GetAsync(url).Result;
                var content = response.Content.ReadAsStringAsync().Result;
                var results = JsonConvert.DeserializeObject<SearchResult>(content);
                return results.Data.Any();
            }
        }

        private AuthResponse FetchAccessToken()
        {
            var parameters = new Dictionary<string, object> {
				{ "client_id", ClientId },
				{ "client_secret", ClientIdSecret },
				{"grant_type", "password"},
				{"scope", "read write"},
				{"username", Username},
				{"password", Password},
			};
            var accessUrl = GetUrl("/api/oauth2/token", parameters);
            using (var client = new HttpClient())
            {
                var response = client.GetAsync(accessUrl).Result;
                var content = response.Content.ReadAsStringAsync().Result;
                var auth = JsonConvert.DeserializeObject<AuthResponse>(content);
                return auth;
            }
        }
        
        public string GetUrl(string resource, IEnumerable<KeyValuePair<string, object>> parameters = null)
        {
            var apiCallUrl = new UriBuilder(Host);
            apiCallUrl.Path += resource;

            var finalParameters = new Dictionary<string, string>();

            if (parameters != null)
                foreach (var parameter in parameters)
                    finalParameters.Add(parameter.Key, parameter.Value.ToString());

            apiCallUrl.Query = string.Join(
                "&",
                (from parameter in finalParameters select (parameter.Key + "=" + HttpUtility.UrlEncode(parameter.Value))).ToArray()
            );

            return apiCallUrl.ToString();
        }
				
    }
}
