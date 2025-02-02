﻿using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WIA;

namespace ScannerDemo
{
    public partial class Form1 : Form
    {
        private static readonly HttpClient client = new HttpClient();
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ListScanners();

            // Set start output folder TMP
            textBox1.Text = Path.GetTempPath();
            // Set JPEG as default
            comboBox1.SelectedIndex = 1;

        }

        private void ListScanners()
        {
            // Clear the ListBox.
            listBox1.Items.Clear();

            // Create a DeviceManager instance
            var deviceManager = new DeviceManager();

            // Loop through the list of devices and add the name to the listbox
            for (int i = 1; i <= deviceManager.DeviceInfos.Count; i++)
            {
                // Add the device only if it's a scanner
                if (deviceManager.DeviceInfos[i].Type != WiaDeviceType.ScannerDeviceType)
                {
                    continue;
                }

                // Add the Scanner device to the listbox (the entire DeviceInfos object)
                // Important: we store an object of type scanner (which ToString method returns the name of the scanner)
                listBox1.Items.Add(
                    new Scanner(deviceManager.DeviceInfos[i])
                );
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Task.Factory.StartNew(StartScanning).ContinueWith(result => TriggerScan());
        }

        private void TriggerScan()
        {
            Console.WriteLine("Image succesfully scanned");
        }

        public void StartScanning()
        {
            Scanner device = null;

            this.Invoke(new MethodInvoker(delegate ()
            {
                device = listBox1.SelectedItem as Scanner;
            }));

            if (device == null)
            {
                Thread newThread = new Thread(new ThreadStart(SelectFromFile));
                newThread.SetApartmentState(ApartmentState.STA);
                newThread.Start();
                //SelectFromFile();
                return;
                MessageBox.Show("You need to select first an scanner device from the list",
                                "Warning",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }else if(String.IsNullOrEmpty(textBox2.Text))
            {
                MessageBox.Show("Provide a filename",
                                "Warning",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ImageFile image = new ImageFile();
            string imageExtension = "";

            this.Invoke(new MethodInvoker(delegate ()
            {
                switch (comboBox1.SelectedIndex)
                {
                    case 0:
                        image = device.ScanImage(WIA.FormatID.wiaFormatPNG);
                        imageExtension = ".png";
                        break;
                    case 1:
                        image = device.ScanImage(WIA.FormatID.wiaFormatJPEG);
                        imageExtension = ".jpeg";
                        break;
                    case 2:
                        image = device.ScanImage(WIA.FormatID.wiaFormatBMP);
                        imageExtension = ".bmp";
                        break;
                    case 3:
                        image = device.ScanImage(WIA.FormatID.wiaFormatGIF);
                        imageExtension = ".gif";
                        break;
                    case 4:
                        image = device.ScanImage(WIA.FormatID.wiaFormatTIFF);
                        imageExtension = ".tiff";
                        break;
                }
            }));
            
            // send image
            Task.Factory.StartNew(() => SendImageAsync(image));
            
            // Save the image
            var path = Path.Combine(textBox1.Text, textBox2.Text + imageExtension);

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            image.SaveFile(path);

            pictureBox1.Image = new Bitmap(path);
        }

        [STAThread]
        private void SelectFromFile()
        {
            var fileContent = string.Empty;
            var filePath = string.Empty;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = "c:\\";
                openFileDialog.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 2;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    //Get the path of specified file
                    filePath = openFileDialog.FileName;

                    //Read the contents of the file into a stream
                    var fileStream = openFileDialog.OpenFile();

                    var binary = ReadFully(fileStream);
                    var response = Upload("http://localhost:5000/ScannedPage", binary);
                    Console.WriteLine(response);
                }
            }
            // Console.WriteLine(fileContent);
            // MessageBox.Show(fileContent, "File Content at path: " + filePath, MessageBoxButtons.OK);
        }

        public static byte[] ReadFully(Stream input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private async Task SendImageAsync(ImageFile image)
        {
            var imageBytes = (byte[])image.FileData.get_BinaryData();
            var response = Upload("http://localhost:5000/ScannedPage", imageBytes);
            Console.WriteLine(response);
        }

        private async Task<string> Upload(string actionUrl, byte[] paramFileBytes)
        {
            //HttpContent stringContent = new StringContent(paramString);
            //HttpContent fileStreamContent = new StreamContent(paramFileStream);
            HttpContent bytesContent = new ByteArrayContent(paramFileBytes, 0, paramFileBytes.Length);
            using (var client = new HttpClient())
            using (var formData = new MultipartFormDataContent())
            {
                //formData.Add(stringContent, "param1", "param1");
                //formData.Add(fileStreamContent, "file1", "file1");
                formData.Add(bytesContent, "ImageFile", "image");
                var response = await client.PostAsync(actionUrl, formData);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                return await response.Content.ReadAsStringAsync();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderDlg = new FolderBrowserDialog();
            folderDlg.ShowNewFolderButton = true;
            DialogResult result = folderDlg.ShowDialog();

            if (result == DialogResult.OK)
            {
                textBox1.Text = folderDlg.SelectedPath;
            }
        }
         
    }
}
