namespace GithubBadges.Models
{
    public class BadgeUpdateRequest
    {
        public string UserId { get; set; }
        public string OldName { get; set; }
        public string NewName { get; set; }
    }
}
