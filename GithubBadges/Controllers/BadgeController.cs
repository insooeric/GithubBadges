using GithubBadges.Models;
using GithubBadges.Middlewares;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DotNetEnv;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Management;
using System.Linq;

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
            }
            catch (Exception ex)
            {
                throw new Exception("Error while configuring credentials: Something went wrong :(", ex);
            }
        }

        [HttpPost("upload-badge")]
        [Authorize] // MAKE SURE TO UNCOMMENT THIS
        public async Task<IActionResult> UploadBadgeAsync([FromForm] BadgeUploadRequestModel request)
        {
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

                if (request.UserId.Equals("-default"))
                {
                    return BadRequest(new { Message = "You are not allowed to change default badge" });
                }

                var validExtensions = new[] { ".png", ".jpg", ".jpeg", ".svg" };
                if (request.BadgeName.Contains("."))
                {
                    return BadRequest(new { Message = "Badge name shouldn't have extension or \".\"" });
                }

                var fileExtension = Path.GetExtension(request.BadgeFile.FileName).ToLower();
                if (!validExtensions.Contains(fileExtension))
                {
                    return BadRequest(new { Message = "Only .png, .jpg, .jpeg, and .svg files are allowed." });
                }

                string userBucketName = request.UserId;
                string fileName = request.BadgeName;

                var credential = GoogleCredential.FromJson(JsonGoogleCred);
                StorageClient storageClient = await StorageClient.CreateAsync(credential);

                string prefix = $"{request.UserId}/";
                var objects = storageClient.ListObjectsAsync(BucketName, prefix);

                if (!await Validator.CheckValidName(Path.GetFileNameWithoutExtension(fileName), JsonGoogleCred))
                {
                    return BadRequest(new { Message = "One of the default badges has the following name. Please choose another name." });
                }

                await foreach (var obj in objects)
                {
                    string fileNameOnly = Path.GetFileNameWithoutExtension(obj.Name.Substring(prefix.Length));
                    if (fileNameOnly.Equals(request.BadgeName, StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest(new { Message = "File name already exists." });
                    }
                }

                string finalSVG = ImageHelper.ConvertToSVG(request.BadgeFile, fileName);

                // Console.WriteLine(finalSVG);

                // return BadRequest(new { Message = $"Error: Testing" }); // MAKE SURE TO REMOVE THIS LINE

                string fullPath = $"{userBucketName}/{fileName}.svg";

                string contentType = "image/svg+xml";

                byte[] finalSVGBytes = System.Text.Encoding.UTF8.GetBytes(finalSVG);

                using (var uploadStream = new MemoryStream(finalSVGBytes))
                {
                    await storageClient.UploadObjectAsync(BucketName, fullPath, contentType, uploadStream);
                }

                string gcs_url = $"https://storage.cloud.google.com/{BucketName}/{fullPath}";

                return Ok(new { Message = "File has been uploaded successfully", PublicURL = gcs_url });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = $"Error while uploading badge {ex.Message}" });
            }
        }

        [HttpGet("get-all-default-badge")]
        public async Task<IActionResult> GetAllDefaultBadgeAsync()
        {
            try
            {
                string userBucketName = $"-default";

                var credential = GoogleCredential.FromJson(JsonGoogleCred);
                StorageClient storageClient = await StorageClient.CreateAsync(credential);
                string prefix = $"{userBucketName}";
                var allObjects = storageClient.ListObjectsAsync(BucketName, prefix);

                var badgeList = new List<BadgeObject>();
                await foreach (var storageObject in allObjects)
                {
                    var jsonString = JsonConvert.SerializeObject(storageObject, Formatting.Indented);
                    string fileName = Path.GetFileNameWithoutExtension(storageObject.Name.Replace($"{userBucketName}/", ""));
                    badgeList.Add(new BadgeObject
                    {
                        UserId = userBucketName,
                        BadgeName = fileName,
                        BadgeURL = $"https://githubbadges.onrender.com/api/badge?badge={fileName}"
                    });
                }

                if (badgeList.Count > 0)
                {
                    badgeList.RemoveAt(0);
                }

                return Ok(new
                {
                    Message = "Retrieved default badges successfully.",
                    Badges = badgeList
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        [HttpPost("get-all-badge")]
        [Authorize]
        public async Task<IActionResult> GetAllBadgeAsync([FromBody] BadgeGetRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { Message = "Request body is missing." });
                }


                if (string.IsNullOrEmpty(request.UserId))
                {
                    return BadRequest(new { Message = "User ID is required." });
                }

                if (string.IsNullOrEmpty(JsonGoogleCred))
                {
                    return BadRequest(new { Message = "Server configuration error: Missing credentials." });
                }

                string userBucketName = $"{request.UserId}";

                var credential = GoogleCredential.FromJson(JsonGoogleCred);
                StorageClient storageClient = await StorageClient.CreateAsync(credential);
                string prefix = $"{userBucketName}";
                var allObjects = storageClient.ListObjectsAsync(BucketName, prefix);

                var badgeList = new List<BadgeObject>();
                await foreach (var storageObject in allObjects)
                {
                    var jsonString = JsonConvert.SerializeObject(storageObject, Formatting.Indented);
                    // Console.WriteLine(jsonString);
                    string fileName = Path.GetFileNameWithoutExtension(storageObject.Name.Replace($"{userBucketName}/", ""));
                    string fileExtension = Path.GetExtension(storageObject.Name);
                    badgeList.Add(new BadgeObject
                    {
                        UserId = userBucketName,
                        BadgeName = fileName,
                        BadgeURL = $"https://githubbadges.onrender.com/api/badge?user={userBucketName}&badge={fileName}",
                        ImageType = fileExtension
                    });
                }



                return Ok(new
                {
                    Message = "Retrieved badges successfully.",
                    Badges = badgeList
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        [HttpPost("update-badge")]
        [Authorize]
        public async Task<IActionResult> UpdateBadgeAsync([FromBody] BadgeUpdateRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { Message = "Request body is missing." });
                }

                if (string.IsNullOrEmpty(request.UserId))
                {
                    return BadRequest(new { Message = "User ID is required." });
                }

                if (string.IsNullOrEmpty(request.OldName))
                {
                    return BadRequest(new { Message = "OldName is required." });
                }

                if (string.IsNullOrEmpty(request.NewName))
                {
                    return BadRequest(new { Message = "NewName is required." });
                }


                if (request.UserId.Equals("-default"))
                {
                    return BadRequest(new { Message = "You are not allowed to update default badge" });
                }

                var credential = GoogleCredential.FromJson(JsonGoogleCred);
                StorageClient storageClient = await StorageClient.CreateAsync(credential);

                string prefix = $"{request.UserId}/";
                var objects = storageClient.ListObjectsAsync(BucketName, prefix);

                if (!await Validator.CheckValidName(Path.GetFileNameWithoutExtension(request.NewName), JsonGoogleCred))
                {
                    return BadRequest(new { Message = $"One of the default badge has the following name. Please choose another name." });
                }

                await foreach (var obj in objects)
                {
                    string fileNameOnly = Path.GetFileNameWithoutExtension(obj.Name.Substring(prefix.Length));

                    if (fileNameOnly.Equals(request.NewName))
                    {
                        return BadRequest(new { Message = $"File name already exists." });
                    }
                }

                string userBucketName = request.UserId;
                string oldObjectPrefix = $"{userBucketName}/{request.OldName}";
                var matchingObjects = storageClient.ListObjectsAsync(BucketName, oldObjectPrefix);
                Google.Apis.Storage.v1.Data.Object oldBadgeObject = null;

                await foreach (var file in matchingObjects)
                {
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.Name);
                    if (fileNameWithoutExtension.Equals(request.OldName))
                    {
                        oldBadgeObject = file;
                        break;
                    }
                }

                if (oldBadgeObject == null)
                {
                    return NotFound(new { Message = "Badge not found." });
                }

                string oldExtension = Path.GetExtension(oldBadgeObject.Name);
                string newObjectName = $"{userBucketName}/{request.NewName}{oldExtension}";

                try
                {
                    storageClient.CopyObject(
                        sourceBucket: BucketName,
                        sourceObjectName: oldBadgeObject.Name,
                        destinationBucket: BucketName,
                        destinationObjectName: newObjectName
                    );

                    await storageClient.DeleteObjectAsync(BucketName, oldBadgeObject.Name);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { Message = $"Error while copying or deleting badge: {ex.Message}" });
                }

                return Ok(new { Message = "Badge has been updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Unexpected error: {ex.Message}" });
            }
        }


        [HttpDelete("delete-badge")]
        [Authorize]
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

                if (request.UserId.Equals("-default"))
                {
                    return BadRequest(new { Message = "You are not allowed to delete default badge" });
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
        // example: https://localhost:32769/api/badge?user=insooeric&badge=auth
        public async Task<IActionResult> GetBadgeAsync([FromQuery] string? user, [FromQuery] string badge, [FromQuery] int? row, [FromQuery] int? col, [FromQuery] bool? fit)
        {
            try
            {
                string userFolderName = string.IsNullOrEmpty(user) ? "-default" : user;
                int definedRow = row ?? 0;
                int definedCol = col ?? 0;
                bool defineFitContent = fit ?? false;

                if (definedRow < 0 || definedCol < 0)
                {
                    return BadRequest(new { Message = "Either row or column cannot be less than 1" });
                }


                if (string.IsNullOrEmpty(badge))
                    return BadRequest(new { Message = "Badge name is required." });

                List<ImageObject>? imageList = new List<ImageObject>();

                string[] imageNameArr = badge.Split(",");
                foreach (string imageName in imageNameArr)
                {
                    string newImageName = imageName.Replace(" ", "");
                    if (string.IsNullOrWhiteSpace(imageName))
                    {
                        return BadRequest(new { Message = "Badge name is required." });
                    }

                    imageList.Add(
                        new ImageObject
                        {
                            imageName = newImageName
                        }
                    );
                }

                var credential = GoogleCredential.FromJson(JsonGoogleCred);
                StorageClient storageClient = await StorageClient.CreateAsync(credential);

                foreach (var item in imageList)
                {


                    /*                    if (!userFolderName.Equals("-default"))
                                        {*/
                    // if folder name isn't default, find in that folder first
                    if (!userFolderName.Equals("-default"))
                    {
                        // Console.WriteLine($"Checking {userFolderName} for {item.imageName}");
                        string objectLongName = $"{userFolderName}/{item.imageName}.svg";
                        try
                        {
                            item.imageObject = await storageClient.GetObjectAsync(BucketName, objectLongName);
                        }
                        catch (Google.GoogleApiException ex) when (ex.Error.Code == 404)
                        {
                            item.imageObject = null;
                        }

                        if (item.imageObject != null)
                        {
                            item.folderName = userFolderName;

                            // ok, we need to reconfigure this to make sure memoryStream is in initial state
                            using var memoryStream = new MemoryStream();
                            await storageClient.DownloadObjectAsync(item.imageObject, memoryStream);
                            item.imageInByte = memoryStream.ToArray();
                            item.imageInSvg = Encoding.UTF8.GetString(item.imageInByte);
                            //Console.WriteLine($"\nLoaded SVG:\n{item.imageInSvg}\n");

                            string ext = Path.GetExtension(item.imageObject.Name).ToLower(); // should be .svg
                            item.imageExtension = ext;
                        }
                    }
                    // if image object is still null, find in default
                    // since upperblock checkes if userFolderName is not -default
                    // it will automatically fallback to here:
                    // IT"S NOT FKING FALLING BACK <= resolved by adding try catch

                    if (item.imageObject == null)
                    {
                        // Console.WriteLine($"Cannot find in {userFolderName}. Checking Default");
                        string objectLongName = $"-default/{item.imageName}.svg";
                        try
                        {
                            item.imageObject = await storageClient.GetObjectAsync(BucketName, objectLongName);
                        }
                        catch (Google.GoogleApiException ex) when (ex.Error.Code == 404)
                        {
                            item.imageObject = null;
                        }

                        item.folderName = "-default";


                        using var memoryStream = new MemoryStream();
                        await storageClient.DownloadObjectAsync(item.imageObject, memoryStream);
                        item.imageInByte = memoryStream.ToArray();

                        // ok... so we're gonna grab svg
                        item.imageInSvg = Encoding.UTF8.GetString(item.imageInByte);
                        // Console.WriteLine($"\nLoaded SVG:\n{item.imageInSvg}\n");
                        // the thing is svg might have different format. it might not contain rect, width etc...
                        // what if we parse svg to image then add svg container and image tag for it?
                        // the result svg should look the same as non-default svg

                        string tmpSvg = new string(Encoding.UTF8.GetString(item.imageInByte));
                        byte[] tmpImg = ImageHelper.ConvertSvgToPng(tmpSvg);
                        string base64Image = Convert.ToBase64String(tmpImg);
                        int newHeight = 100;
                        int newWidth = ImageHelper.GetWidthByHeight(newHeight, tmpImg);

                        string newTmpSvg = $@"
<svg xmlns=""http://www.w3.org/2000/svg"" width=""{newWidth}px"" height=""{newHeight}px"" x=""0"" y=""0"" fill=""none"">
  <defs>
    <clipPath id=""clip-{item.imageName}"">
      <rect width=""100%"" height=""100%"" rx=""8"" />
    </clipPath>
  </defs>
  <image href=""data:image/png;base64,{base64Image}"" width=""100%"" height=""100%"" 
         clip-path=""url(#clip-{item.imageName})"" preserveAspectRatio=""xMidYMid meet"" />
</svg>";

                        // Console.WriteLine(newTmpSvg);
                        item.imageInSvg = newTmpSvg;

                        // Console.WriteLine($"\nLoaded SVG:\n{item.imageInSvg}\n");
                        // THAT FUCING WORKS!

                        string ext = Path.GetExtension(item.imageObject.Name).ToLower(); // should be .svg
                        item.imageExtension = ext;
                    }

                    // if image object is still null, return error
                    if (item.imageObject == null)
                    {
                        return BadRequest(new { Message = $"Could not find the badge named \"{item.imageName}\"" });
                    }
                }

                /*                Console.WriteLine("---------------------------------------------");
                                Console.WriteLine("List of Images");
                                foreach (var image in imageList)
                                {
                                    Console.WriteLine(image.imageInSvg);
                                    Console.WriteLine();
                                }
                                Console.WriteLine("---------------------------------------------");*/


                /*                foreach (var item in imageList)
                                {
                                    Console.WriteLine($"----------------------");
                                    Console.WriteLine($"Object exists? {(item.imageObject != null ? "Yes" : "No")}");
                                    Console.WriteLine($"Image byte exists?: {(item.imageInByte != null ? "Yes" : "No")}");
                                    Console.WriteLine($"Image svg exists?: {(item.imageInSvg != null ? "Yes" : "No")}");
                                    Console.WriteLine($"Image folder: {item.folderName}");
                                    Console.WriteLine($"Image name: {item.imageName}");
                                    Console.WriteLine($"Image type: {item.imageExtension}\n");
                                }*/
                


                string svgContent = "";

                if (imageList.Count == 1)
                {
                    // svgContent = Encoding.UTF8.GetString(imageList[0].imageInByte);

                    // Console.WriteLine($"\nLoaded SVG:\n{imageList[0].imageInSvg}\n");
                    //svgContent = ImageHelper.ResizeSVG(imageList[0].imageInSvg, ImageHelper.GetWidthByHeight(40, imageList[0].imageInSvg), 40);
                    int newHeight = 40;
                    int newWidth = ImageHelper.GetWidthByHeight(newHeight, imageList[0].imageInSvg);
                    //Console.WriteLine($"New dimension:\nWidth: {newWidth}\nHeight: {newHeight}");
                    svgContent = ImageHelper.ResizeSVG(imageList[0].imageInSvg, newWidth, newHeight); //imageList[0].imageInSvg;
                    // BOOM!
                }
                else if (imageList.Count > 1) // in case multiple images
                {
                    /*                    Console.WriteLine("---------------------------------------------");
                                        Console.WriteLine("Render multiple images");
                                        foreach (var image in imageList)
                                        {
                                            Console.WriteLine(image.imageInSvg);
                                            Console.WriteLine();
                                        }*/
                    // TODO: THIS IS A ROUGH VERSION. NEED TO ADD ENHANCED LOGIC
                    // do it in MultipleSVGCreator.Create()
                    if (definedRow > imageList.Count || definedCol > imageList.Count)
                    {
                        return BadRequest(new { Message = $"Error: both Row and Column cannot exceed number of badges" });
                    }
                    try
                    {
                        svgContent = MultipleSVGCreator.Create(imageList, definedRow, definedCol, defineFitContent);
                    }
                    catch (ArgumentException ex)
                    {
                        return BadRequest(new { Message = $"Error: {ex.Message}" });
                    }
                }

                return Content(svgContent, "image/svg+xml");
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = $"Error while grabbing badge {ex.Message}" });
            }
        }
    }
}