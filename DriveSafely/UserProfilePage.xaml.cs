﻿using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace DriveSafely
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class UserProfilePage : Page
    {
        private Driver currentUser;
        private Image[] userIDImages;
        private double idImageMaxWidth = 0;

        private WebcamHelper webcam;

        public UserProfilePage()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Triggered every time the page is navigated to.
        /// </summary>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            try
            {
                // Catches the passed UserProfilePage parameters
                UserProfileObject userProfileParameters = e.Parameter as UserProfileObject;

                // Sets current user as the passed through Driver object
                currentUser = userProfileParameters.Driver;
                // Sets the DriverNameBlock as the current user's name
                DriverNameBlock.Text = currentUser.Name;

                // Sets the local WebcamHelper as the passed through intialized one
                webcam = userProfileParameters.WebcamHelper;
            }
            catch
            {
                // Something went wrong... It's likely the page was navigated to without a Driver parameter. Navigate back to MainPage
                Frame.Navigate(typeof(MainPage));
            }
        }

        private void PhotoGrid_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // Populate photo grid with Driver ID photos:
            PopulatePhotoGrid();
        }

        /// <summary>
        /// Populates PhotoGrid on UserProfilePage.xaml with photos in ImageFolder of passed through Driver object
        /// </summary>
        private async void PopulatePhotoGrid()
        {
            // Sets max width to allow 6 photos to sit in one row
            idImageMaxWidth = PhotoGrid.ActualWidth / 6 - 10;

            var filesInFolder = await currentUser.ImageFolder.GetFilesAsync();

            userIDImages = new Image[filesInFolder.Count];

            for (int i = 0; i < filesInFolder.Count; i++)
            {
                var photoStream = await filesInFolder[i].OpenAsync(FileAccessMode.Read);
                BitmapImage idImage = new BitmapImage();
                await idImage.SetSourceAsync(photoStream);

                Image idImageControl = new Image();
                idImageControl.Source = idImage;
                idImageControl.MaxWidth = idImageMaxWidth;

                userIDImages[i] = idImageControl;
            }

            PhotoGrid.ItemsSource = userIDImages;
        }

        /// <summary>
        /// Triggered when the user clicks the add photo button located in the app bar
        /// </summary>
        private async void AddButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Captures photo from current webcam stream
            StorageFile imageFile = await webcam.CapturePhoto();
            // Moves the captured file to the current user's ID image folder
            await imageFile.MoveAsync(currentUser.ImageFolder);
            // Update photo grid
            PopulatePhotoGrid();
            // Add to Oxford
            OxfordFaceAPIHelper.AddImageToWhitelist(imageFile, currentUser.Name);
        }

        /// <summary>
        /// Triggered when the user clicks the delete user button located in the app bar
        /// </summary>
        private async void DeleteButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Delete the user's folder
            await currentUser.ImageFolder.DeleteAsync();

            // Remove user from Oxford
            OxfordFaceAPIHelper.RemoveUserFromWhitelist(currentUser.Name);

            // Stop camera preview
            await webcam.StopCameraPreview();

            // Navigate to MainPage
            Frame.Navigate(typeof(MainPage));
        }

        /// <summary>
        /// Triggered when the user clicks the home button located in the app bar
        /// </summary>
        private async void HomeButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Stop camera preview
            await webcam.StopCameraPreview();

            // Navigate to MainPage
            Frame.Navigate(typeof(MainPage));
        }
    }
}

