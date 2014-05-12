using System.Collections.Generic;
using Seq.Apps.LogEvents;

namespace Seq.App.Ontime
{
    public class OnTimeIncident
    {
        public Incident Item { get; set; }
    }

    public class Incident
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public User Assigned_To { get; set; }
        public Project Project { get; set; }
        public string Notes { get; set; }
        public Priotity Priority { get; set; }
    }

    public class ProjectList
    {
        public List<Project> Data { get; set; }
    }

    public class Project
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<Project> Children { get; set; }
    }

    public class AuthResponse
    {
        public string Access_Token { get; set; }
        public User Data { get; set; }
    }

    public class SearchResult
    {
        public List<Incident> Data { get; set; }
    }

    public class User
    {
        public int Id { get; set; }
    }

    public class Priotity
    {
        public int Id { get; set; }

        public static Priotity FromDebugLevel(LogEventLevel level)
        {
            int priority = 4; // high

            if (level == LogEventLevel.Information || level == LogEventLevel.Warning || level == LogEventLevel.Verbose)
            {
                priority = 2; // low
            }

            return new Priotity { Id = priority };
        }
    }
}