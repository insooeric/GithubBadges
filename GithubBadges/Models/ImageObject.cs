namespace GithubBadges.Models
{
    public class ImageObject
    {
        public Google.Apis.Storage.v1.Data.Object imageObject;

        public byte[] imageInByte;

        public string folderName;
        public string imageName;
        public string imageExtension;
    }
}
