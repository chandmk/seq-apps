using System.Collections.Generic;

namespace Seq.App.Ontime
{
    public class OnTimeDefect
    {
        public Defect Item { get; set; }
    }

    public class Defect
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public User Assigned_To { get; set; }
        public Project Project { get; set; }
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
        public List<Defect> Data { get; set; }
    }

    public class User
    {
        public int Id { get; set; }
    }
}