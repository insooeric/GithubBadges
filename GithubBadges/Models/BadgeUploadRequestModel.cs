namespace GithubBadges.Models
{
    public class BadgeUploadRequestModel
    {
        public IFormFile BadgeFile { get; set; }
        public string BadgeName { get; set; }
        public string UserId { get; set; }
    }
}
