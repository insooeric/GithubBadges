using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;

namespace GithubBadges.Middlewares
{
    public static class Validator
    {

        private static readonly string BucketName = "badge-bucket";

        public static async Task<bool> CheckValidName(string name, string JsonGoogleCred)
        {
            // Console.WriteLine($"Checking name \"{name}\"");
            var credential = GoogleCredential.FromJson(JsonGoogleCred);
            StorageClient storageClient = await StorageClient.CreateAsync(credential);

            string prefix = $"-default/";
            var matchingObjects = storageClient.ListObjectsAsync(BucketName, prefix);

            await foreach (var obj in matchingObjects)
            {
                if (Path.GetFileNameWithoutExtension(obj.Name).Equals(name))
                {
                    return false;
                }
                // count++;
            }
            return true;
        }
    }
}
