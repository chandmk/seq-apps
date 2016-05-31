using Newtonsoft.Json;
using Seq.Apps;
using Seq.Apps.LogEvents;
using System;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Seq.App.Jira
{

    [SeqApp("JIRA Incident",
    Description = "Posts seq event an incident in JIRA")]
    public class JiraIssueReactor : Reactor, ISubscribeTo<LogEventData>
    {
        /// <summary>
        ///  Seq Server Address
        /// </summary>
        /// <value>
        ///     Seq server address
        /// </value>
        [SeqAppSetting(
            DisplayName = "Seq Server Address",
            HelpText = "URL of the seq server. This appears in description field so that you can get back to the event from JIRA.")]
        public string SeqUrl { get; set; }

        /// <summary>
        ///     Gets the host.
        /// </summary>
        /// <value>
        ///     The host.
        /// </value>
        [SeqAppSetting(
            DisplayName = "Jira Host (url)",
            HelpText = "URL of Jira (do not include /rest/api/ at the end of the path).")]
        public string Host { get; set; }

        /// <summary>
        ///     Gets the name of the project.
        /// </summary>
        /// <value>
        ///     The name of the project.
        /// </value>
        [SeqAppSetting(
            DisplayName = "Project Key",
            IsOptional = false,
            HelpText = "Project Key to post Jira issue.")]
        public string ProjectKey { get; set; }

        /// <summary>
        ///     Gets the custom field in JIRA that stores SeqEventId.
        /// </summary>
        /// <value>
        ///     The username.
        /// </value>
        [SeqAppSetting(
            DisplayName = "Seq Event Id custom field # from JIRA",
            IsOptional = false,
            HelpText = "Jira custome field number to store Seq Event Id")]
        public int SeqEventField { get; set; }

        /// <summary>
        ///     Gets the custom field in JIRA that stores SeqEventId.
        /// </summary>
        /// <value>
        ///     The username.
        /// </value>
        [SeqAppSetting(
            DisplayName = "Jira issue type",
            IsOptional = false,
            HelpText = "Jira issue type")]
        public string JiraIssueType { get; set; }


        /// <summary>
        ///     Gets the username.
        /// </summary>
        /// <value>
        ///     The username.
        /// </value>
        [SeqAppSetting(
            DisplayName = "Username",
            IsOptional = false,
            HelpText = "Authenticated username for Jira.")]
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
            HelpText = "Authenticated Password for Jira.",
            InputType = SettingInputType.Password)]
        public string Password { get; set; }

        public void On(Event<LogEventData> evt)
        {
            try
            {
                PostIncident(evt);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while creating incident in JIRA");
            }
        }


        public void PostIncident(Event<LogEventData> evt)
        {
            var message = evt.Data.Exception ?? evt.Data.RenderedMessage;
            var messageId = ComputeId(message);

            using (HttpClient client = new HttpClient())
            {
                var baseurl = Host + "/rest/api/latest/";
                client.BaseAddress = new Uri(baseurl);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(Username + ":" + Password)));

                var geturl = string.Format("search?jql=project={0}+AND+cf[{1}]~{2}&maxResults=1&fields=id,key,summary,customfield_{1}", ProjectKey, SeqEventField, messageId);
                var getresponse = client.GetStringAsync(geturl).Result;
                var searchResults = JsonConvert.DeserializeObject<JiraIssueSearch>(getresponse);
                Console.WriteLine(getresponse);

                if (searchResults.total == 0)
                {
                    var subject = evt.Data.RenderedMessage;
                    if (!string.IsNullOrEmpty(subject))
                    {
                        var nlindex = subject.IndexOf('\n');
                        if(nlindex > 0)
                        {
                            subject = subject.Substring(0, nlindex);
                        }
                        int max = subject.Length > 120 ? 120 : subject.Length;
                        subject = subject.Substring(0, max);
                    }
                    var body = string.Format(@"*Timestamp*:{0}
                                *Level*:{1}
                                *ExceptionEventId*:{2}
                                *Exception*:
                                {3}
                    ",evt.TimestampUtc.ToLocalTime(), evt.Data.Level, evt.Id, evt.Data.Exception);
                    var surl = string.Format("[Seq Event {0}|{1}/#/events?filter=@Id%20%3D%3D%20%22{0}%22]", evt.Id, SeqUrl);
                    var description = string.Format(@"{0}

                                        {1}", body, surl);
                    var cf = "customfield" + "_" + SeqEventField;
                    var fields = new
                    {
                        project = new { key = ProjectKey },
                        summary = subject,
                        description = System.Security.SecurityElement.Escape(description),
                        issuetype = new { name = JiraIssueType },
                        seqeventfield = messageId
                    };
                    var issueText = JsonConvert.SerializeObject(new { fields = fields });
                    issueText = issueText.Replace("seqeventfield", cf);
                    Console.WriteLine(issueText);
                    var jiraIssue = JsonConvert.DeserializeObject<Issue>(issueText);
                    var response = client.PostAsync<Issue>("issue", jiraIssue, new JsonMediaTypeFormatter()).Result;
                    if (!response.IsSuccessStatusCode)
                    {
                        var err = response.Content.ReadAsStringAsync().Result;
                        throw new ApplicationException(response.ReasonPhrase + "" + err);
                    }
                }
            }
        }

        private static string ComputeId(string input)
        {
            MD5 md5 = MD5.Create();
            return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(input))).Replace("-", string.Empty);
        }
    }
}


public class JiraIssueSearch
{
    public int maxResults { get; set; }
    public int total { get; set; }
    public Issue[] issues { get; set; }
}

public class Issue
{
    public string id { get; set; }
    public string key { get; set; }
    public Fields fields { get; set; }
}

public class Fields
{
    public Fields()
    {
        project = new Project();
        issuetype = new Issuetype() { name = "Exception Log" };
    }
    public Project project { get; set; }
    public string summary { get; set; }
    public string description { get; set; }
    public Issuetype issuetype { get; set; }
    public string customfield_10425 { get; set; }
}

public class Project
{
    public string key { get; set; }
}

public class Issuetype
{
    public string name { get; set; }
}
