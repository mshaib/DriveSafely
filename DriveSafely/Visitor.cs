using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace DriveSafely
{
    /// <summary>
    /// Contains information such as name and location of ID images in storage for each authorized Driver
    /// </summary>
    class Driver
    {
        /// <summary>
        /// Name of the person
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The image folder
        /// </summary>
        public StorageFolder ImageFolder { get; set; }

        /// <summary>
        /// The person's image
        /// </summary>
        public BitmapImage Image { get; set; }

        /// <summary>
        /// The width used for each ID photo on the photo grid on MainPage.xaml
        /// </summary>
        public double MaxWidth { get; set; }

        /// <summary>
        /// Initializes a new Driver object with relevant information
        /// </summary>
        public Driver(string name, StorageFolder imageFolder, BitmapImage image, double maxWidth)
        {
            Name = name;
            ImageFolder = imageFolder;
            Image = image;
            MaxWidth = maxWidth;
        }
    }
}


