namespace DriveSafely
{
    /// <summary>
    /// Object specifically to be passed to UserProfilePage that contains an instance of the WebcamHelper and a Driver object
    /// </summary>
    class UserProfileObject
    {
        /// <summary>
        /// An initialized Driver object
        /// </summary>
        public Driver Driver { get; set; }

        /// <summary>
        /// An initialized WebcamHelper 
        /// </summary>
        public WebcamHelper WebcamHelper { get; set; }

        /// <summary>
        /// Initializes a new UserProfileObject with relevant information
        /// </summary>
        public UserProfileObject(Driver driver, WebcamHelper webcamHelper)
        {
            Driver = driver;
            WebcamHelper = webcamHelper;
        }
    }
}

