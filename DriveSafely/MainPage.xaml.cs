using DriveSafely.FacialRecognition;
using System.Runtime.InteropServices.WindowsRuntime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

using Windows.Devices.Gpio;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using System.Collections.ObjectModel;

using System.Threading;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth.Rfcomm;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;

using Microsoft.WindowsAzure.Storage.Table;


namespace DriveSafely
{
    public sealed partial class MainPage : Page
    {

        private DispatcherTimer timer;
        // Webcam Related Variables:
        private WebcamHelper webcam;

        //Shut app variables
        Boolean done = false;
        int numOfTries = 0;
        

        // Oxford Related Variables:
        private bool initializedOxford = false;

        // Whitelist Related Variables:
        private List<Driver> whitelistedDrivers = new List<Driver>();
        private StorageFolder whitelistFolder;
        private bool currentlyUpdatingWhitelist;

        // Unknown Related Variables:
        private List<Driver> UnknownDrivers = new List<Driver>();
        private StorageFolder UnknownsFolder;
        private bool currentlyUpdatingUnknowns;

        //Car Status Variables:
        // temporary ( the car is off ) 
        Boolean carStatus = false;
        Boolean ConnectedToOBD = false;
        long RpmResponse;
        String recTextFromObd;

        //Bluetooth related Variables:
        private Windows.Devices.Bluetooth.Rfcomm.RfcommDeviceService _service;
        private StreamSocket _socket;
        private DataWriter dataWriterObject;
        private DataReader dataReaderObject;
        ObservableCollection<PairedDeviceInfo> _pairedDevices;
        private CancellationTokenSource ReadCancellationTokenSource;


        // GUI Related Variables:
        private double DriverIDPhotoGridMaxWidth = 0;
        private double UnknownIDPhotoGridMaxWidth = 0;

        //Azure Storage variables
        private readonly string Azure_StorageAccountName = "drivesafelystoragetable";
        private readonly string Azure_ContainerName = "driversdatabase";
        private readonly string Azure_AccessKey = "1JuR9258ar2ikK7f58C71m0x/AzAIJEO+jkKr6u2vDFFZ1DXdmoOawvVCsaumJE1ZoH6FyLOUyv4WCksaS2frg==";

        //Status LED variables
        private const int LED_PIN = 5;
        private GpioPin PinLED;


        /// <summary>
        ///  Class to hold all paired device information
        /// </summary>
        public class PairedDeviceInfo
        {
            internal PairedDeviceInfo(DeviceInformation deviceInfo)
            {
                this.DeviceInfo = deviceInfo;
                this.ID = this.DeviceInfo.Id;
                this.Name = this.DeviceInfo.Name;
            }

            public string Name { get; private set; }
            public string ID { get; private set; }
            public DeviceInformation DeviceInfo { get; private set; }
        }
        /// <summary>
        /// Called when the page is first navigated to.
        /// </summary>
        public MainPage()
        {
            InitializeComponent();

            // Causes this page to save its state when navigating to other pages
            NavigationCacheMode = NavigationCacheMode.Enabled;


            // Check to see if Oxford facial recongition has been initialized
            if (initializedOxford == false)
            {
                // If not, attempt to initialize it
                InitializeOxford();
                Debug.WriteLine("oxford has been initialized");
            }

            InitializeGPIO();

            // If user has set the DisableLiveCameraFeed within Constants.cs to true, disable the feed.
            if (GeneralConstants.DisableLiveCameraFeed)
            {
                LiveFeedPanel.Visibility = Visibility.Collapsed;
                DisabledFeedGrid.Visibility = Visibility.Visible;
            }
            else
            {
                LiveFeedPanel.Visibility = Visibility.Visible;
                DisabledFeedGrid.Visibility = Visibility.Collapsed;
            }
/*
            // Initialize The bluetooth connection
            // Enter mode 0100 in the OBD and start listen 
            InitializeRfcommDeviceService();

            // Initilize The timer for 500 Milliseconds 
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(20);
            timer.Tick += Capture_Driver;
            timer.Start();*/
        }
        // After finding the OBDII ,this function make the Pairing 
        async private void connectDev()
        {
            //Revision: No need to requery for Device Information as we alraedy have it:
            DeviceInformation DeviceInfo; // = await DeviceInformation.CreateFromIdAsync(this.TxtBlock_SelectedID.Text);
            PairedDeviceInfo pairedDevice = (PairedDeviceInfo)ConnectDevices.SelectedItem;
            DeviceInfo = pairedDevice.DeviceInfo;

            bool success = true;

            try
            {
                _service = await RfcommDeviceService.FromIdAsync(DeviceInfo.Id);
                if (_socket != null)
                {
                    // Disposing the socket with close it and release all resources associated with fthe socket
                    _socket.Dispose();
                }

                _socket = new StreamSocket();
                try
                {
                    // Note: If either parameter is null or empty, the call will throw an exception
                    await _socket.ConnectAsync(_service.ConnectionHostName, _service.ConnectionServiceName);
                }
                catch (Exception ex)
                {
                    success = false;
                    System.Diagnostics.Debug.WriteLine("Bluetooth Connect:" + ex.Message);
                }
                // If the connection was successful, the RemoteAddress field will be populated
                if (success)
                {
                    string msg = String.Format("Connected to {0}!", _socket.Information.RemoteAddress.DisplayName);
                    //MessageDialog md = new MessageDialog(msg, Title);
                    System.Diagnostics.Debug.WriteLine(msg);
                    //await md.ShowAsync();
                    ConnectedToOBD = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Overall Connect: " + ex.Message);
                _socket.Dispose();
                _socket = null;
            }

        }

        /*  
        *  Initialize Bluetooth select The OBDII from all paired devices (Paired manually to the raspberryPi) and paired it 
        *  Enter mode 0100 in the OBD by sending the OBD The command 010C and start listen for any response from the OBD.
        */
        async void InitializeRfcommDeviceService()
        {
            await Task.Delay(TimeSpan.FromSeconds(30));
            Debug.WriteLine("Initializing Bluetooth Connections.");
            try
            {
                DeviceInformationCollection DeviceInfoCollection = await DeviceInformation.FindAllAsync(RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort));

                // Get the number of the paired devices to the RaspberryPi
                var numDevices = DeviceInfoCollection.Count();

                // By clearing the backing data, we are effectively clearing the ListBox
                _pairedDevices = new ObservableCollection<PairedDeviceInfo>();
                _pairedDevices.Clear();

                // If there is no paired devices to The raspberryPi 
                if (numDevices == 0)
                {
                    //MessageDialog md = new MessageDialog("No paired devices found", "Title");
                    //await md.ShowAsync();
                    System.Diagnostics.Debug.WriteLine("InitializeRfcommDeviceService: No paired devices found.");
                }
                // Found some paired devices.
                else
                {
                    foreach (var deviceInfo in DeviceInfoCollection)
                    {
                        _pairedDevices.Add(new PairedDeviceInfo(deviceInfo));
                    }
                }
                PairedDevices.Source = _pairedDevices;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("InitializeRfcommDeviceService: " + ex.Message);
            }
            // select The OBD device and pair it 
            for (int i = 0; i < ConnectDevices.Items.Count(); i++)
            {
                PairedDeviceInfo currDev = (PairedDeviceInfo)ConnectDevices.Items[i];
                if (currDev.Name.Equals("OBDII"))
                {
                    //connecting to OBD
                    connectDev();
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
            }

            // successfully connected to OBD.
            if (ConnectedToOBD == true)
            {
                RpmResponse = 0;
                // Entering mode 0100 
                Send("010C\r");
                await Task.Delay(TimeSpan.FromSeconds(10));
                // start listen for any response from the obd
                Listen();
                if (recTextFromObd.Substring(5, 2).Equals("41"))
                {
                    //translating the received data from the OBD
                    string tmp1, tmp2;
                    tmp1 = recTextFromObd.Substring(11, 2);
                    tmp2 = recTextFromObd.Substring(14, 2);
                    RpmResponse = Convert.ToInt64(string.Concat(tmp1, tmp2), 16);
                    carStatus = true;
                    Debug.WriteLine("RpmResonse : " + RpmResponse);
               //     await SomeoneEntered();


                }
                else if (string.Equals(recTextFromObd.Substring(0, 1), "N", StringComparison.OrdinalIgnoreCase) || string.Equals(recTextFromObd.Substring(0, 1), "C", StringComparison.OrdinalIgnoreCase))
                {
                    carStatus = false;
                }
                else
                {
                    // OBD no response - Emergency mode
                    RpmResponse = -1;
                }
            }
        }

        

        public async void Send(string msg)
        {
            try
            {
                if (_socket.OutputStream != null)
                {
                    // Create the DataWriter object and attach to OutputStream
                    dataWriterObject = new DataWriter(_socket.OutputStream);

                    //Launch the WriteAsync task to perform the write
                    await WriteAsync(msg);
                }
                else
                {
                    Debug.WriteLine("Select a device and connect");
                }
            }
            catch (Exception ex)
            {
                //status.Text = "Send(): " + ex.Message;
                System.Diagnostics.Debug.WriteLine("Send(): " + ex.Message);
            }
            finally
            {
                // Cleanup once complete
                if (dataWriterObject != null)
                {
                    dataWriterObject.DetachStream();
                    dataWriterObject = null;
                }
            }
        }

        /// <summary>
        /// WriteAsync: Task that asynchronously writes data from the input text box 'sendText' to the OutputStream 
        /// </summary>
        /// <returns></returns>
        private async Task WriteAsync(string msg)
        {
            Task<UInt32> storeAsyncTask;

            if (msg == "")
                msg = "none";// sendText.Text;
            if (msg.Length != 0)
            //if (msg.sendText.Text.Length != 0)
            {
                // Load the text from the sendText input text box to the dataWriter object
                dataWriterObject.WriteString(msg);

                // Launch an async task to complete the write operation
                storeAsyncTask = dataWriterObject.StoreAsync().AsTask();

                UInt32 bytesWritten = await storeAsyncTask;
                if (bytesWritten > 0)
                {
                    string status_Text = msg + ", ";
                    status_Text += bytesWritten.ToString();
                    status_Text += " bytes written successfully!";
                    System.Diagnostics.Debug.WriteLine(status_Text);
                }
            }
            else
            {
                string status_Text2 = "Enter the text you want to write and then click on 'WRITE'";
                System.Diagnostics.Debug.WriteLine(status_Text2);
            }
        }

        /// <summary>
        /// - Create a DataReader object
        /// - Create an async task to read from the SerialDevice InputStream
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Listen()
        {
            try
            {
                ReadCancellationTokenSource = new CancellationTokenSource();
                if (_socket.InputStream != null)
                {
                    dataReaderObject = new DataReader(_socket.InputStream);
                    
                    // keep reading the serial input
                    while (true)
                    {
                        await ReadAsync(ReadCancellationTokenSource.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.GetType().Name == "TaskCanceledException")
                {
                    System.Diagnostics.Debug.WriteLine("Listen: Reading task was cancelled, closing device and cleaning up");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Listen: " + ex.Message);
                }
            }
            finally
            {
                // Cleanup once complete
                if (dataReaderObject != null)
                {
                    dataReaderObject.DetachStream();
                    dataReaderObject = null;
                }
            }
        }


        /// <summary>
        /// ReadAsync: Task that waits on data and reads asynchronously from the serial device InputStream
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task ReadAsync(CancellationToken cancellationToken)
        {
            Task<UInt32> loadAsyncTask;

            uint ReadBufferLength = 1024;

            // If task cancellation was requested, comply
            cancellationToken.ThrowIfCancellationRequested();

            // Set InputStreamOptions to complete the asynchronous read operation when one or more bytes is available
            dataReaderObject.InputStreamOptions = InputStreamOptions.Partial;

            // Create a task object to wait for data on the serialPort.InputStream
            loadAsyncTask = dataReaderObject.LoadAsync(ReadBufferLength).AsTask(cancellationToken);

            // Launch the task and wait
            UInt32 bytesRead = await loadAsyncTask;
            if (bytesRead > 0)
            {
                try
                {
                    string recvdtxt = dataReaderObject.ReadString(bytesRead);
                    recTextFromObd = recvdtxt;
                    Debug.WriteLine(recvdtxt);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("ReadAsync: " + ex.Message);
                }

            }
        }

        /// <summary>
        /// Triggered every time the page is navigated to.
        /// </summary>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (initializedOxford)
            {
                UpdateWhitelistedDrivers();
            }

            UpdateUnknownDrivers();
        }

        /// <summary>
        /// Called once, when the app is first opened. Initializes Oxford facial recognition.
        /// </summary>
        public async void InitializeOxford()
        {
            // initializedOxford bool will be set to true when Oxford has finished initialization successfully
            initializedOxford = await OxfordFaceAPIHelper.InitializeOxford();

            // Populates UI grid with whitelisted Drivers
            UpdateWhitelistedDrivers();

            // Populates UI grid with Unknowns
            UpdateUnknownDrivers();
        }

        /// <summary>
        /// Called once, when the app is first opened. Initializes device GPIO.
        /// </summary>
        public void InitializeGpio()
        {
           
        }

        /// <summary>
        /// Triggered when webcam feed loads both for the first time and every time this page is navigated to.
        /// If no WebcamHelper has been created, it creates one. Otherwise, simply restarts webcam preview feed on page.
        /// </summary>
        private async void WebcamFeed_Loaded(object sender, RoutedEventArgs e)
        {
            if (webcam == null || !webcam.IsInitialized())
            {
                // Initialize Webcam Helper
                webcam = new WebcamHelper();
                await webcam.InitializeCameraAsync();

                // Set source of WebcamFeed on MainPage.xaml
                WebcamFeed.Source = webcam.mediaCapture;

                // Check to make sure MediaCapture isn't null before attempting to start preview. Will be null if no camera is attached.
                if (WebcamFeed.Source != null)
                {
                    // Start the live feed
                    await webcam.StartCameraPreview();
                }
            }
            else if (webcam.IsInitialized())
            {
                WebcamFeed.Source = webcam.mediaCapture;

                // Check to make sure MediaCapture isn't null before attempting to start preview. Will be null if no camera is attached.
                if (WebcamFeed.Source != null)
                {
                    await webcam.StartCameraPreview();
                }
            }
        }

       

        /// <summary>
        /// Triggered when the whitelisted users grid is loaded. Sets the size of each photo within the grid.
        /// </summary>
        private void WhitelistedUsersGrid_Loaded(object sender, RoutedEventArgs e)
        {
            DriverIDPhotoGridMaxWidth = (WhitelistedUsersGrid.ActualWidth / 3) - 10;
        }

        /// <summary>
        /// Triggered when the Unknowns grid is loaded. Sets the size of each photo within the grid.
        /// </summary>
        private void UnknownsGrid_Loaded(object sender, RoutedEventArgs e)
        {
            UnknownIDPhotoGridMaxWidth = (UnknownsGrid.ActualWidth / 3) - 10;
        }

        /// <summary>
        /// Triggered when user presses virtual button on the app bar
        /// </summary>
        private async void CameraButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(carStatus && RpmResponse!=-1 &&ConnectedToOBD==true))
            {
                //Car turned on
                carStatus = true;
                ConnectedToOBD = true;
                RpmResponse = 1;
                await SomeoneEntered();
            }
        }

        /// <summary>
        /// Called when someone the car is on or vitual capture button is pressed.
        /// Captures photo of current webcam view and sends it to Oxford for facial recognition processing.
        /// </summary>
        private async Task SomeoneEntered()
        {
            

            // List to store Drivers recognized by Oxford Face API
            // Count will be greater than 0 if there is an authorized Driver at the door
            List<string> recognizedDrivers = new List<string>();

            // Confirms that webcam has been properly initialized and oxford is ready to go
            if (webcam.IsInitialized() && initializedOxford)
            {
                // Stores current frame from webcam feed in a temporary folder
                StorageFile UnknownImage = await webcam.CapturePhoto();

                try
                {
                    // Oxford determines whether or not the Driver is on the Whitelist and returns recongized Driver if so
                    recognizedDrivers = await OxfordFaceAPIHelper.IsFaceInWhitelist(UnknownImage);
                }
                catch (FaceRecognitionException fe)
                {
                    switch (fe.ExceptionType)
                    {
                        // Fails and catches as a FaceRecognitionException if no face is detected in the image
                        case FaceRecognitionExceptionType.NoFaceDetected:
                            Debug.WriteLine("WARNING: No face detected in this image.");
                            break;
                    }
                }
                catch
                {
                    // General error. This can happen if there are no Drivers authorized in the whitelist
                    Debug.WriteLine("WARNING: Oxford just threw a general expception.");
                }

                if (recognizedDrivers.Count > 0)
                {
                    // If everything went well and a Driver was recognized, allow the person entry
                    driverDetected(recognizedDrivers[0]);
             
                    //TODO: turn red light
                }
                else
                {
                    Debug.WriteLine("undetected driver");

                    // If the UnknownsFolder has not been opened, open it
                    if (UnknownsFolder == null)
                    {
                        // Create the UnknownsFolder if it doesn't exist; if it already exists, open it.
                        UnknownsFolder = await KnownFolders.PicturesLibrary.CreateFolderAsync(GeneralConstants.UnknownFolderName, CreationCollisionOption.OpenIfExists);
                    }

                    // Determine the number of Unknowns already recorded
                    var UnknownSubFolders = await UnknownsFolder.GetFoldersAsync();
                    int UnknownCount = UnknownSubFolders.Count;

                    // Convert the Unknown count integer to string for the subfolder name
                    string subFolderName = "Unknown" + UnknownCount.ToString();

                    // Create a subfolder to store this specific Unknown's photo
                    StorageFolder currentFolder = await UnknownsFolder.CreateFolderAsync(subFolderName, CreationCollisionOption.ReplaceExisting);
                    // Move the already captured photo the Unknown's folder
                    await UnknownImage.MoveAsync(currentFolder);

                    // Refresh the UI grid of Unknowns
                    UpdateUnknownDrivers();
                }
            }
            else
            {
                if (!webcam.IsInitialized())
                {
                    // The webcam has not been fully initialized for whatever reason:
                    Debug.WriteLine("Unable to analyze Driver at door as the camera failed to initlialize properly.");
                }

                if (!initializedOxford)
                {
                    // Oxford is still initializing:
                    Debug.WriteLine("Unable to analyze Driver at door as Oxford Facial Recogntion is still initializing.");
                }
            }     
            if(numOfTries==3)
                done = true;
            numOfTries++;
           
        }

        /// <summary>
        /// Turn LED on and send message to cloud
        /// </summary>
        private async void driverDetected(string driverName)
        {
            
          //  Debug.WriteLine("Driver Detected: "+ driverName);
            String cloudMessage = "Driver Detected: " + driverName + " Date and Time: " + DateTime.Now.ToString();
             await UploadDriverDetails(driverName);
            Debug.WriteLine(cloudMessage);
           // await UploadDriverDetails(cloudMessage);
            LightLED(true);
            await Task.Delay(TimeSpan.FromSeconds(5));
            LightLED(false);
            done = true;
        }

        /// <summary>
        /// Called when user hits vitual add user button. Navigates to NewUserPage page.
        /// </summary>
        private async void NewUserButton_Click(object sender, RoutedEventArgs e)
        {
            // Stops camera preview on this page, so that it can be started on NewUserPage
            await webcam.StopCameraPreview();

            //Navigates to NewUserPage, passing through initialized WebcamHelper object
            Frame.Navigate(typeof(NewUserPage), webcam);
        }

        /// <summary>
        /// Updates internal list of whitelisted Drivers (whitelistedDrivers) and the visible UI grid
        /// </summary>
        private async void UpdateWhitelistedDrivers()
        {
            // If the whitelist isn't already being updated, update the whitelist
            if (!currentlyUpdatingWhitelist)
            {
                currentlyUpdatingWhitelist = true;
                await UpdateWhitelistedDriversList();
                UpdateWhitelistedDriversGrid();
                currentlyUpdatingWhitelist = false;
            }
        }

        /// <summary>
        /// Updates the list of Driver objects with all whitelisted Drivers stored on disk
        /// </summary>
        private async Task UpdateWhitelistedDriversList()
        {
            // Clears whitelist
            whitelistedDrivers.Clear();

            // If the whitelistFolder has not been opened, open it
            if (whitelistFolder == null)
            {
                // Create the whitelistFolder if it doesn't exist; if it already exists, open it.
                whitelistFolder = await KnownFolders.PicturesLibrary.CreateFolderAsync(GeneralConstants.WhiteListFolderName, CreationCollisionOption.OpenIfExists);
            }

            // Populate subFolders list with all sub folders within the whitelist folder.
            // Each of these sub folders represents the Id photos for a single Driver.
            var whitelistSubFolders = await whitelistFolder.GetFoldersAsync();

            // Iterate all subfolders in whitelist
            foreach (StorageFolder folder in whitelistSubFolders)
            {
                // Get each Driver's name from the folder name
                string whitelistDriverName = folder.Name;

                // Get the files from each folder
                var filesInWhitelistFolder = await folder.GetFilesAsync();

                // Use the first photo in the folder as the Drivers image for the whitelist
                var whitelistPhotoStream = await filesInWhitelistFolder[0].OpenAsync(FileAccessMode.Read);
                BitmapImage DriverImage = new BitmapImage();
                await DriverImage.SetSourceAsync(whitelistPhotoStream);

                // Create the Driver object will all the information about the Driver
                Driver whitelistedDriver = new Driver(whitelistDriverName, folder, DriverImage, DriverIDPhotoGridMaxWidth);

                // Add the Driver to the white list
                whitelistedDrivers.Add(whitelistedDriver);
            }
        }

        /// <summary>
        /// Updates UserInterface list of whitelisted users from the list of Driver objects (whitelistedDrivers)
        /// </summary>
        private void UpdateWhitelistedDriversGrid()
        {
            // Reset source to empty list
            WhitelistedUsersGrid.ItemsSource = new List<Driver>();

            // Set source of WhitelistedUsersGrid to the whitelistedDrivers list
            WhitelistedUsersGrid.ItemsSource = whitelistedDrivers;

           
        }

        /// <summary>
        /// Triggered when the user selects a Driver in the WhitelistedUsersGrid 
        /// </summary>
        private void WhitelistedUsersGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Navigate to UserProfilePage, passing through the selected Driver object and the initialized WebcamHelper as a parameter
            Frame.Navigate(typeof(UserProfilePage), new UserProfileObject(e.ClickedItem as Driver, webcam));
        }

        /// <summary>
        /// Updates maintained list of Unknowns (UnknownDrivers) and the visible UI grid
        /// </summary>
        private async void UpdateUnknownDrivers()
        {
            // If the list of Unknowns isn't already being updated, update the list
            if (!currentlyUpdatingUnknowns)
            {
                currentlyUpdatingUnknowns = true;
                await UpdateUnknownDriversList();
                UpdateUnknownDriversGrid();
                currentlyUpdatingUnknowns = false;
            }
        }

        /// <summary>
        /// Updates the list of Driver objects with all whitelisted Drivers stored on disk
        /// </summary>
        private async Task UpdateUnknownDriversList()
        {
            // Clear list of Unknowns
            UnknownDrivers.Clear();

            // If the UnknownsFolder has not been opened, open it
            if (UnknownsFolder == null)
            {
                // Create the UnknownsFolder if it doesn't exist; if it already exists, open it.
                UnknownsFolder = await KnownFolders.PicturesLibrary.CreateFolderAsync(GeneralConstants.UnknownFolderName, CreationCollisionOption.OpenIfExists);
            }

            // Populates UnknownSubFolders list with all sub folders within the Unknown folder.
            // Each of these sub folders represents the Id photos for a single Unknown.
            var UnknownSubFolders = await UnknownsFolder.GetFoldersAsync();

            // Iterate all subfolders in whitelist
            foreach (StorageFolder folder in UnknownSubFolders)
            {
                // Get each Driver's name from the folder name
                string UnknownName = folder.Name;

                // Get the files from each folder
                var filesInUnknownFolder = await folder.GetFilesAsync();

                // Use the first photo in the folder as the Drivers image for the whitelist
                var UnknownPhotoStream = await filesInUnknownFolder[0].OpenAsync(FileAccessMode.Read);
                BitmapImage UnknownImage = new BitmapImage();
                await UnknownImage.SetSourceAsync(UnknownPhotoStream);

                // Create the Driver object will all the information about the Driver
                Driver UnknownDriver = new Driver(UnknownName, folder, UnknownImage, UnknownIDPhotoGridMaxWidth);

                // Add the Driver to the white list
                UnknownDrivers.Add(UnknownDriver);
            }
        }

        /// <summary>
        /// Updates UserInterface list of Unknowns from the list of Driver objects (UnknownDrivers)
        /// </summary>
        private void UpdateUnknownDriversGrid()
        {
            // Reset source to empty list
            UnknownsGrid.ItemsSource = new List<Driver>();

            // Set source of WhitelistedUsersGrid to the whitelistedDrivers list
            UnknownsGrid.ItemsSource = UnknownDrivers;

           
        }

        /// <summary>
        /// Triggered when the user selects an Unknown in the UnknownsGrid 
        /// </summary>
        private void UnknownsGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Navigate to UnknownProfilePage, passing through the selected Driver object and the initialized WebcamHelper as a parameter
            Frame.Navigate(typeof(UnknownProfilePage), new UserProfileObject(e.ClickedItem as Driver, webcam));
        }

        private async void Capture_Driver(object sender, object e)
        {
            // Exit app
            if (done)
                Application.Current.Exit();

            await Task.Delay(TimeSpan.FromSeconds(120));
            
           
      
            //The car is off 
            if (RpmResponse <1 && ConnectedToOBD == true)
            {
                Debug.WriteLine("The car is turned off");
                await Task.Delay(TimeSpan.FromSeconds(10));
                Send("010C\r");
                await Task.Delay(TimeSpan.FromSeconds(10));
                // start listen for any response from the obd
                Listen();
                if (recTextFromObd.Substring(5, 2).Equals("41"))
                {
                    //translating the received data from the OBD
                    string tmp1, tmp2;
                    tmp1 = recTextFromObd.Substring(11, 2);
                    tmp2 = recTextFromObd.Substring(14, 2);
                    RpmResponse = Convert.ToInt64(string.Concat(tmp1, tmp2), 16);
                    carStatus = true;
                    Debug.WriteLine("RpmResonse : " + RpmResponse);
                }
            }
            // The car is ON 
            else if (carStatus == true && RpmResponse >0 && ConnectedToOBD == true)
            {
               await SomeoneEntered();
            }
            else 
            {
                Debug.WriteLine("something went wrong with OBD Connection");
                Debug.WriteLine("Trying to initialize bluetooth device");
                InitializeRfcommDeviceService(); 
            }
        }


        /// <summary>
        /// Initialize the GPIO ports on the Raspberry Pi
        /// 
        /// GPIO PIN 16 = PIR Signal
        /// GPIO PIN  5 = LED Status
        /// </summary>
        private void InitializeGPIO()
        {
            try
            {
                //Obtain a reference to the GPIO Controller
                var gpio = GpioController.GetDefault();

                // Show an error if there is no GPIO controller
                if (gpio == null)
                {
                    PinLED = null;
                    Debug.WriteLine("No GPIO controller found on this device.");
                    return;
                }

                //Open the GPIO port for LED
                PinLED = gpio.OpenPin(LED_PIN);

                //set the mode as Output (we are WRITING a signal to this port)
                PinLED.SetDriveMode(GpioPinDriveMode.Output);


                Debug.WriteLine("GPIO pins " + LED_PIN.ToString() + " initialized correctly.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("GPIO init error: " + ex.Message);
            }

        }



        /// <summary>
        /// Toggles LOW/HIGH values to LED's GPIO port
        /// to turn LED either on or off
        /// </summary>
        private void LightLED(bool show = true)
        {
            if (PinLED == null)
                return;

            if (show)
            {
                PinLED.Write(GpioPinValue.Low);
            }
            else
            {
                PinLED.Write(GpioPinValue.High);
            }
        }


        /// <summary>
        /// Upload the StorageFile to Azure Blob Storage
        /// </summary>
        /// <param name="file">The StorageFile to upload</param>
        /// <returns>null</returns>
        private async Task UploadDriverDetails(String name)
        {
            try
            {
                StorageCredentials creds1 = new StorageCredentials(Azure_StorageAccountName, Azure_AccessKey);
                CloudStorageAccount account1 = new CloudStorageAccount(creds1, useHttps: true);
                // Create the table client.
                CloudTableClient tableClient = account1.CreateCloudTableClient();
                // Create the CloudTable object that represents the "people" table.
                CloudTable table = tableClient.GetTableReference("driversHistory");

                // Create a new customer entity.
                DriversEntity Driver = new DriversEntity(name+ DateTime.Now.ToString(), name+ DateTime.Now.ToString());
                Driver.DriversName = name;
                Driver.DriveDate = DateTime.Now;

                // Create the TableOperation object that inserts the customer entity.
                TableOperation insertOperation = TableOperation.Insert(Driver);

                // Execute the insert operation.
               await table.ExecuteAsync(insertOperation);
                

            }
            catch (Exception ex)
            {
                Debug.WriteLine("uploading error" + ex.Message);
            }

           
           
        }

        public class DriversEntity : TableEntity
        {
            public DriversEntity(string lastName, string firstName)
            {
                this.PartitionKey = lastName;
                this.RowKey = firstName;
            }

            public DriversEntity() { }

            public string DriversName { get; set; }

            public DateTime DriveDate{ get; set; }
        }

        /// <summary>
        /// Triggered when the user selects the Shutdown button in the app bar. Closes app.
        /// </summary>
        private void ShutdownButton_Click(object sender, RoutedEventArgs e)
        {
            // Exit app
            Application.Current.Exit();
        }

    }
}
