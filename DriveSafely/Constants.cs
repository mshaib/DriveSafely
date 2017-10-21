namespace DriveSafely
{
    /// <summary>
    /// General constant variables
    /// </summary>
    public static class GeneralConstants
    {
        // With no GPU support, the Raspberry Pi cannot display the live camera feed so this variable should be set to true.
        // However, if you are deploying to other harware on which Windows 10 IoT Core does have GPU support, set it to false.
        public const bool DisableLiveCameraFeed = true;

        // Oxford Face API Primary should be entered here
        // You can obtain a subscription key for Face API by following the instructions here: https://www.microsoft.com/cognitive-services/en-us/sign-up
        public const string OxfordAPIKey = "01f9f796782d44458e3aabac5075bf04";

        // Name of the folder in which all Whitelist data is stored
        public const string WhiteListFolderName = "Drivers_Whitelist";

        // Name of the folder in which all the Unknown data is stored
        public const string UnknownFolderName = "Driver Unknowns";
    }
}

