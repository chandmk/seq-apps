using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.Ontime
{
    public partial class OntimeTicketReactor : Reactor, ISubscribeTo<LogEventData>
    {
        /// <summary>
        ///     Gets the host.
        /// </summary>
        /// <value>
        ///     The host.
        /// </value>
        [SeqAppSetting(
            DisplayName = "Host (url)",
            HelpText = "URL of the ontime instance (do not include http:// or path).")]
        public string Host { get; set; }

        
        /// <summary>
        ///     Gets the full pathname of the file.
        /// </summary>
        /// <value>
        ///     The full pathname of the file.
        /// </value>
        [SeqAppSetting(
            DisplayName = "Path",
            IsOptional = true,
            HelpText = "Defaults to none. Additional path on OnTime URL.")]
        public string Path { get; set; }

        /// <summary>
        ///     Gets the name of the project.
        /// </summary>
        /// <value>
        ///     The name of the project.
        /// </value>
        [SeqAppSetting(
            DisplayName = "Project Id",
            IsOptional = false,
            HelpText = "Project Id to post OnTime issue.")]
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

                if (DefectAlreadyExistsInOntime(evt.Id))
                {
                    return;
                }

                PostDefect(evt);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while creating a defect in Ontime");
            }
          
        }

        AuthResponse AuthorizedUser { get; set; }

        private void PostDefect(Event<LogEventData> evt)
        {
            var body = FormatDefaultBody(evt);

            var subject = evt.Id + " - " + evt.Data.RenderedMessage;

            var defect = new Defect
            {
                Name = subject,
                Description = body,
                Project = new Project { Id = ProjectId },
                Assigned_To = new User {Id = AuthorizedUser.Data.Id}
            };
            var ontimeDefect = new OnTimeDefect
            {
                Item = defect
            };
            var url = "/api/v2/defects?access_token=" + AuthorizedUser.Access_Token;
            using (var client = new HttpClient())
            {
                 client.PostAsJsonAsync(url, ontimeDefect);
            }
        }

        private bool DefectAlreadyExistsInOntime(string id)
        {
            var parameters = new Dictionary<string, object> {
				{ "project_id", ProjectId },
				{ "search_field", "name" },
				{"search_string", id},
				{"columns", id},
			};
            var url = GetUrl("/api/v2/defects?access_token=" + AuthorizedUser.Access_Token, parameters);
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

        string FormatDefaultBody(Event<LogEventData> evt)
        {
            var body = new StringBuilder();
            body.Append("{{@Timestamp}} [{{@Level}}] {{@RenderedMessage}}");

            if (evt.Data.Properties != null)
            {
                body.AppendLine();

                foreach (var property in evt.Data.Properties.OrderBy(p => p.Key))
                {
                    body.AppendFormat(" {0} = {{{{{1}}}}}", property.Key, property.Key);
                    body.AppendLine();
                }
            }

            if (evt.Data.Exception != null)
            {
                body.AppendLine();
                body.Append("{{@Exception}}");
            }

            return FormatTemplate(body.ToString(), evt);
        }

        string FormatTemplate(string template, Event<LogEventData> evt)
        {
            var tokens = StacheParser.ParseStache(template);
            var output = new StringWriter();
            foreach (var tok in tokens)
                tok.Render(output, evt);
            return output.ToString();
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
