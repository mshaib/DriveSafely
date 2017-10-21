using DriveSafely.FacialRecognition;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;

namespace DriveSafely
{
    /// <summary>
    /// Allows easy access to Oxford functions such as adding a Driver to whitelist and checking to see if a Driver is on the whitelist
    /// </summary>
    static class OxfordFaceAPIHelper
    {
        /// <summary>
        /// Initializes Oxford API. Builds existing whitelist or creates one if one does not exist.
        /// </summary>
        public async static Task<bool> InitializeOxford()
        {
            
            // Attempts to open whitelist folder, or creates one
            StorageFolder whitelistFolder = await KnownFolders.PicturesLibrary.CreateFolderAsync(GeneralConstants.WhiteListFolderName, CreationCollisionOption.OpenIfExists);

            // Creates a new instance of the Oxford API Controller
            FaceApiRecognizer sdkController = FaceApiRecognizer.Instance;

            // Attempts to open whitelist ID file, or creates one
            StorageFile WhiteListIdFile = await whitelistFolder.CreateFileAsync("WhiteListId.txt", CreationCollisionOption.OpenIfExists);

            // Reads whitelist file to get whitelist ID and stores value
            string savedWhitelistId = await FileIO.ReadTextAsync(WhiteListIdFile);

            // If the ID has not been created, creates a whitelist ID
            if (savedWhitelistId == "")
            {
                string id = Guid.NewGuid().ToString();
                await FileIO.WriteTextAsync(WhiteListIdFile, id);
                savedWhitelistId = id;
            }

            // Builds whitelist from exisiting whitelist folder
            await sdkController.CreateWhitelistFromFolderAsync(savedWhitelistId, whitelistFolder, null);

            // Return true to indicate that Oxford was initialized successfully
            return true;
        }

        /// <summary>
        /// Accepts a user name and the folder in which their identifying photos are stored. Adds them to the whitelist.
        /// </summary>
        public async static void AddUserToWhitelist(string name, StorageFolder photoFolder)
        {
            try
            {
                // Acquires instance of Oxford SDK controller
                FaceApiRecognizer sdkController = FaceApiRecognizer.Instance;
                // Asynchronously adds user to whitelist
                await sdkController.AddPersonToWhitelistAsync(photoFolder, name);
            }
            catch (FaceRecognitionException e)
            {
                Debug.WriteLine(e.StackTrace);
            }
            catch (Exception e1)
            {
                Debug.WriteLine("Failed to add user to whitelist.");
                Debug.WriteLine(e1.Message + "\n\n" + e1.StackTrace);
            }
           

        }

        /// <summary>
        /// Accepts an image file and the name of a Driver. Associates photo with exisiting Driver.
        /// </summary>
        public async static void AddImageToWhitelist(StorageFile imageFile, string name)
        {
            try
            {
                // Acquires instance of Oxford SDK controller
                FaceApiRecognizer sdkController = FaceApiRecognizer.Instance;
                // Asynchronously adds image to whitelist
                await sdkController.AddImageToWhitelistAsync(imageFile, name);
            }
            catch(Exception e)
            {

                Debug.WriteLine("Failed to add image.");
                Debug.WriteLine(e.StackTrace);
            }
        }

        /// <summary>
        /// Accepts the name of a Driver. Removes them from whitelist.
        /// </summary>
        public async static void RemoveUserFromWhitelist(string name)
        {
            // Acquires instance of Oxford SDK controller
            FaceApiRecognizer sdkController = FaceApiRecognizer.Instance;
            // Asynchronously remove user from whitelist
            await sdkController.RemovePersonFromWhitelistAsync(name);
        }

        /// <summary>
        /// Checks to see if a whitelisted Driver is in passed through image. Returns list of whitelisted Drivers. If no authorized users are detected, returns an empty list.
        /// </summary>
        public async static Task<List<string>> IsFaceInWhitelist(StorageFile image)
        {
            FaceApiRecognizer sdkController = FaceApiRecognizer.Instance;
            List<string> matchedImages = new List<string>();
            matchedImages = await sdkController.FaceRecognizeAsync(image);

            return matchedImages;
        }
    }
}

