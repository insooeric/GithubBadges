using GithubBadges.Models;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DotNetEnv;
using Google.Apis.Storage.v1.Data;
using System.Text;

namespace GithubBadges.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BadgeController : ControllerBase
    {
        private readonly string JsonGoogleCred;
        private readonly string BucketName = "badge-bucket";

        public BadgeController()
        {
            try
            {
                Env.Load();

                string privateKey = Environment.GetEnvironmentVariable("GOOGLE_PRIVATE_KEY")?.Replace("\\n", "\n") ?? "";
                var serviceAccountJson = new JObject
                {
                    { "type", Environment.GetEnvironmentVariable("GOOGLE_TYPE") ?? "" },
                    { "project_id", Environment.GetEnvironmentVariable("GOOGLE_PROJECT_ID") ?? "" },
                    { "private_key_id", Environment.GetEnvironmentVariable("GOOGLE_PRIVATE_KEY_ID") ?? "" },
                    { "private_key", privateKey },
                    { "client_email", Environment.GetEnvironmentVariable("GOOGLE_CLIENT_EMAIL") ?? "" },
                    { "client_id", Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? "" },
                    { "auth_uri", Environment.GetEnvironmentVariable("GOOGLE_AUTH_URI") ?? "" },
                    { "token_uri", Environment.GetEnvironmentVariable("GOOGLE_TOKEN_URI") ?? "" },
                    { "auth_provider_x509_cert_url", Environment.GetEnvironmentVariable("GOOGLE_AUTH_PROVIDER_X509_CERT_URL") ?? "" },
                    { "client_x509_cert_url", Environment.GetEnvironmentVariable("GOOGLE_CLIENT_X509_CERT_URL") ?? "" },
                    { "universe_domain", Environment.GetEnvironmentVariable("GOOGLE_UNIVERSE_DOMAIN") ?? "" }
                };

                this.JsonGoogleCred = JsonConvert.SerializeObject(serviceAccountJson);

                if (string.IsNullOrEmpty(JsonGoogleCred))
                {
                    throw new Exception("Error while configuring credentials: Credentials undefined :(");
                }
            } catch (Exception ex)
            {
                throw new Exception("Error while configuring credentials: Something went wrong :(", ex);
            }
        }

        [HttpPost("upload-badge")]
        public async Task<IActionResult> UploadBadgeAsync([FromForm] BadgeUploadRequestModel request)
        {
            Env.Load();
            try
            {
                if (request.BadgeFile == null || request.BadgeFile.Length <= 0)
                {
                    return BadRequest(new { Message = "Badge image not found" });
                }

                if (string.IsNullOrEmpty(request.BadgeName))
                {
                    return BadRequest(new { Message = "Badge name is required" });
                }

                if (string.IsNullOrEmpty(request.UserId))
                {
                    return BadRequest(new { Message = "User Id is required" });
                }

                var validExtensions = new[] { ".png", ".jpg", ".jpeg" };
                var fileExtension = Path.GetExtension(request.BadgeFile.FileName).ToLower();
                if (!validExtensions.Contains(fileExtension))
                {
                    return BadRequest(new { Message = "Only .png, .jpg, and .jpeg files are allowed." });
                }

                string userBucketName = $"{request.UserId}";
                string fileName = $"{request.BadgeName}";
                // fileExtension

                var credential = GoogleCredential.FromJson(JsonGoogleCred);
                StorageClient storageClient = await StorageClient.CreateAsync(credential);

                string prefix = $"{request.UserId}/";
                var objects = storageClient.ListObjectsAsync(BucketName, prefix);

                await foreach (var obj in objects)
                {
                    string fileNameOnly = Path.GetFileNameWithoutExtension(obj.Name.Substring(prefix.Length));
                    // Console.WriteLine($"File base name: {fileNameOnly}");

                    if (fileNameOnly.Equals(request.BadgeName))
                    {
                        return BadRequest(new { Message = $"File name already exists." });
                    }
                }

                string fullPath = userBucketName + "/" + fileName + fileExtension;
                using (var stream = request.BadgeFile.OpenReadStream())
                {
                    await storageClient.UploadObjectAsync(BucketName, fullPath, null, stream);
                } // ok, this works


                string gcs_url = $"https://storage.cloud.google.com/{BucketName}/{fullPath}";

                return Ok(new { Message = $"File has been uploaded successfully", PublicURL = gcs_url });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = $"Error while uploading badge {ex.Message}" });
            }
        }

        [HttpDelete("delete-badge")]
        public async Task<IActionResult> DeleteBadgeAsync([FromBody] BadgeDeleteRequest request)
        {
            Env.Load();

            try
            {
                if (request == null)
                {
                    return BadRequest(new { Message = "Request body is missing." });
                }

                if (string.IsNullOrEmpty(request.BadgeName))
                {
                    return BadRequest(new { Message = "Badge name is required." });
                }

                if (string.IsNullOrEmpty(request.UserId))
                {
                    return BadRequest(new { Message = "User ID is required." });
                }

                string userBucketName = $"{request.UserId}";
                string fileName = $"{request.BadgeName}";



                if (string.IsNullOrEmpty(JsonGoogleCred))
                {
                    return BadRequest(new { Message = "Server configuration error: Missing credentials." });
                }

                var credential = GoogleCredential.FromJson(JsonGoogleCred);
                StorageClient storageClient = await StorageClient.CreateAsync(credential);

                string prefix = $"{userBucketName}/{fileName}";

                var matchingObjects = storageClient.ListObjectsAsync(BucketName, prefix);
                Google.Apis.Storage.v1.Data.Object badgeObject = null;

                await foreach(var file in matchingObjects)
                {
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.Name);
                    if (fileNameWithoutExtension.Equals(fileName))
                    {
                        badgeObject = file;
                        break;
                    }
                }

                if (badgeObject == null)
                {
                    return NotFound(new { Message = "Badge not found." });
                }

                try
                {
                    await storageClient.DeleteObjectAsync(BucketName, badgeObject.Name);
                }
                catch (Google.GoogleApiException ex) when (ex.Error.Code == 404)
                {
                    return NotFound(new { Message = "Badge not found during deletion." });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { Message = $"Error while deleting badge: {ex.Message}" });
                }

                return Ok(new { Message = "Badge has been deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }


        [HttpGet("")]
        // test https://localhost:32769/api/badge?user=test1234&badge=cat
        //public async Task<IActionResult> GetBadgeAsync([FromBody] BadgeGetRequest request)
        public async Task<IActionResult> GetBadgeAsync([FromQuery] string user, [FromQuery] string badge)
        {
            Env.Load();
            try
            {
                if (string.IsNullOrEmpty(user))
                    return BadRequest(new { Message = "User Id is required." });

                if (string.IsNullOrEmpty(badge))
                    return BadRequest(new { Message = "Badge name is required." });

                // string bucketName = "badge-bucket";
                string userFolderName = user;
                string fileName = badge;

                string privateKey = Environment.GetEnvironmentVariable("GOOGLE_PRIVATE_KEY")?.Replace("\\n", "\n") ?? "";
                var serviceAccountJson = new JObject
                {
                    { "type", Environment.GetEnvironmentVariable("GOOGLE_TYPE") ?? "" },
                    { "project_id", Environment.GetEnvironmentVariable("GOOGLE_PROJECT_ID") ?? "" },
                    { "private_key_id", Environment.GetEnvironmentVariable("GOOGLE_PRIVATE_KEY_ID") ?? "" },
                    { "private_key", privateKey },
                    { "client_email", Environment.GetEnvironmentVariable("GOOGLE_CLIENT_EMAIL") ?? "" },
                    { "client_id", Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? "" },
                    { "auth_uri", Environment.GetEnvironmentVariable("GOOGLE_AUTH_URI") ?? "" },
                    { "token_uri", Environment.GetEnvironmentVariable("GOOGLE_TOKEN_URI") ?? "" },
                    { "auth_provider_x509_cert_url", Environment.GetEnvironmentVariable("GOOGLE_AUTH_PROVIDER_X509_CERT_URL") ?? "" },
                    { "client_x509_cert_url", Environment.GetEnvironmentVariable("GOOGLE_CLIENT_X509_CERT_URL") ?? "" },
                    { "universe_domain", Environment.GetEnvironmentVariable("GOOGLE_UNIVERSE_DOMAIN") ?? "" }
                };

                var jsonCred = JsonConvert.SerializeObject(serviceAccountJson);
                var credential = GoogleCredential.FromJson(JsonGoogleCred);
                StorageClient storageClient = await StorageClient.CreateAsync(credential);

                string prefix = $"{userFolderName}/{fileName}";
                var matchingObjects = storageClient.ListObjectsAsync(BucketName, prefix);
                Google.Apis.Storage.v1.Data.Object badgeObject = null;

                await foreach (var file in matchingObjects)
                {
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.Name);
                    if (fileNameWithoutExtension.Equals(fileName))
                    {
                        badgeObject = file;
                        break;
                    }
                }

                if (badgeObject == null)
                {
                    return NotFound(new { Message = "Badge not found." });
                }
                
                // ok we do have badge image in this point
                // whateverimage => base64img => svg?
                using var memoryStream = new MemoryStream();
                await storageClient.DownloadObjectAsync(badgeObject, memoryStream);
                var imageBytes = memoryStream.ToArray();

                string ext = Path.GetExtension(badgeObject.Name).ToLower();
                string mimeType = (ext == ".jpg" || ext == ".jpeg") ? "image/jpeg" : "image/png";

                string base64Image = Convert.ToBase64String(imageBytes);
                string svgContent = $@"
                    <svg xmlns=""http://www.w3.org/2000/svg"" width=""200"" height=""200"">
                      <image href=""data:{mimeType};base64,{base64Image}"" width=""200"" height=""200"" />
                    </svg>";

                return File(Encoding.UTF8.GetBytes(svgContent), "image/svg+xml");
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = $"Error while grabbing badge {ex.Message}" });
            }
        }
    }
}