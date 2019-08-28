using System;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OneClickF5Login.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Timer = System.Timers.Timer;


namespace OneClickF5Login
{
    public partial class Form1 : Form
    {
        private readonly string DefaultChromeDataPath = @"C:\Users\ibrahim.aydin15\AppData\Local\Google\Chrome\User Data";

        private const string DefaultChromePath = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";
        private ChromeDriver ChromeDriver { get; set; }

        private const string ConfigUid = "{049DD0E0-1751-45B3-B840-84ED1C6A018A}";

        private static string _statusMessage = string.Empty;

        public Form1()
        {
            DefaultChromeDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data");
            InitializeComponent();
            var statusTimer = new Timer()
            {
                Interval = 100
            };
            statusTimer.Elapsed += StatusTimer_Elapsed;
            statusTimer.Start();
            SetInputValuesFromConfiguration();
        }

        private void StatusTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            btnLogin.Invoke(new Action(() => { lblStatus.Text = _statusMessage; }));
        }

        private void SetInputValuesFromConfiguration()
        {
            Program.UnSecureSection();
            var section = Program.GetSettingsSection();
            var coll = section.VpnLoginList[ConfigUid];
            txtPassword.Text = "" + coll?.Password;
            txtUserName.Text = "" + coll?.Username;
            txtChromeDataPath.Text = string.IsNullOrEmpty(coll?.ChromeDataPath) ? DefaultChromeDataPath : coll.ChromeDataPath;
            txtChromePath.Text = string.IsNullOrEmpty(coll?.ChromePath) ? DefaultChromePath : coll.ChromePath;
            txtOtpKey.Text = coll?.OtpKey;
            chkRemember.Checked = (coll?.RememberPassword).GetValueOrDefault();
            chkShowBrowser.Checked = (coll?.ShowBrowser).GetValueOrDefault();
            Program.SecureSection();
        }


        private void BtnLogin_Click(object sender, EventArgs e)
        {
            Task.Run(ConnectAsync);
        }

        private async Task ConnectAsync()
        {
            try
            {
                EnableDisableForm(false);
                _statusMessage = "Değerler doğrulanıyor.";
                ValidateInputs();
                _statusMessage = "VPN durumu kontrol ediliyor.";
                if (PingHost("tfs.saglik.gov.tr"))
                {
                    throw new InvalidOperationException("VPN zaten bağlı.");
                }
                _statusMessage = "Ayarlar kaydediliyor.";
                SaveValuesToConfiguration();
                _statusMessage = "Chrome sürücüsü yükleniyor.";
                var newDataDir = CopyChromeDataDirToTemp(txtChromeDataPath.Text);
                InitializeChromeDriver(txtChromePath.Text, newDataDir, chkShowBrowser.Checked);
                if (chkShowBrowser.Checked)
                {
                    ChromeDriver.Manage().Window.Maximize();
                }
                else
                {
                    ChromeDriver.Manage().Window.Size = new Size(800, 800);
                    ChromeDriver.Manage().Window.Position = new Point(6000, 6000);
                }
                _statusMessage = "Chrome ayarları yapılıyor.";
                Thread.Sleep(1000);
                var cookies = ChromeDriver.Manage().Cookies.AllCookies.Where(i => !string.IsNullOrEmpty(i.Domain) && i.Domain.Contains("sslvpn.saglik.gov.tr")).ToList();
                cookies.ForEach(cookie => ChromeDriver.Manage().Cookies.DeleteCookie(cookie));
                _statusMessage = "Sağlık Bakanlığı SSL VPN sayfası yükleniyor.";
                ChromeDriver.Url = "https://sslvpn.saglik.gov.tr";
                Thread.Sleep(1000);
                await NewSessionIfNecessary();
                _statusMessage = "VPN kullanıcı giriş işlemi gerçekleştiriliyor.";
                await LoginSession(txtUserName.Text, txtPassword.Text);
                _statusMessage = "OTP şifresi oluşturuluyor.";
                await ChooseOtpOption();
                var code = GenerateOtpCode(txtOtpKey.Text);
                _statusMessage = "OTP şifresi gönderiliyor.";
                await SubmitOtpCode(code);
                _statusMessage = "Web VPN bağlantısı başlatılıyor.";
                await StartVpnConnection();
                _statusMessage = "VPN bağlantısı kontrol ediliyor.";
                await WaitUntilPingSuccess(10000);
                _statusMessage = "İşlem başarılı.";
                Thread.Sleep(1000);
                _statusMessage = "";
                EnableDisableForm(true);
                Close();
            }
            catch (Exception exception)
            {
                _statusMessage = "";
                EnableDisableForm(true);
                MessageBox.Show($"{exception.Message}\r\n\r\n\r\n\r\n{exception}");
            }
        }

        private void EnableDisableForm(bool enabled)
        {
            btnLogin.Invoke(new Action(() =>
            {
                btnLogin.Enabled = enabled;
                txtUserName.Enabled = enabled;
                txtChromeDataPath.Enabled = enabled;
                txtChromePath.Enabled = enabled;
                txtOtpKey.Enabled = enabled;
                txtPassword.Enabled = enabled;
                chkShowBrowser.Enabled = enabled;
                chkRemember.Enabled = enabled;
            }));
        }

        private async Task WaitUntilPingSuccess(int maxTimeout)
        {
            DateTime startTime = DateTime.Now;

            while (!PingHost("tfs.saglik.gov.tr"))
            {
                if ((DateTime.Now - startTime).TotalMilliseconds > maxTimeout)
                {
                    return;
                }
                Thread.Sleep(1000);
            }
        }

        public static bool PingHost(string nameOrAddress)
        {
            bool pingable = false;
            Ping pinger = null;

            try
            {
                pinger = new Ping();
                var reply = pinger.Send(nameOrAddress);
                if (reply != null) pingable = reply.Status == IPStatus.Success;
            }
            catch (PingException)
            {
                // Discard PingExceptions and return false;
            }
            finally
            {
                pinger?.Dispose();
            }

            return pingable;
        }

        private void SaveValuesToConfiguration()
        {
            Program.UnSecureSection();
            var section = Program.GetSettingsSection();
            var sett = new VpnLoginConfigurationElement
            {
                Uid = ConfigUid,
                Username = txtUserName.Text,
                RememberPassword = chkRemember.Checked,
                ChromeDataPath = txtChromeDataPath.Text,
                ChromePath = txtChromePath.Text,
                OtpKey = txtOtpKey.Text,
                Password = chkRemember.Checked ? txtPassword.Text : "",
                ShowBrowser = chkShowBrowser.Checked
            };
            section.VpnLoginList[ConfigUid] = sett;
            section.SectionInformation.ForceSave = true;
            section.CurrentConfiguration.Save(ConfigurationSaveMode.Full);
            Program.SecureSection();
        }

        private void ValidateInputs()
        {
            StringBuilder errMessage = new StringBuilder();


            if (string.IsNullOrEmpty(txtUserName.Text))
            {
                errMessage.AppendLine("-- Kullanıcı adınızı giriniz !!");

            }

            if (string.IsNullOrEmpty(txtPassword.Text))
            {
                errMessage.AppendLine("-- Parolanızı giriniz !!!");
            }

            if (string.IsNullOrEmpty(txtChromeDataPath.Text))
            {
                errMessage.AppendLine("-- Chrome Data Path boş değer olamaz !!!");
            }
            else if (!Directory.Exists(txtChromeDataPath.Text))
            {
                errMessage.AppendLine($"-- Chrome Data Path geçersiz. Chrome uygulamasının kullanıcı profil ve ayarlarını sakladığı root klasörü girmelisiniz. Örnek : {DefaultChromeDataPath}");
            }

            if (string.IsNullOrEmpty(txtChromePath.Text))
            {
                errMessage.AppendLine("-- Chrome.exe Path boş değer olamaz !!!");
            }
            else if (!File.Exists(txtChromePath.Text))
            {
                errMessage.AppendLine("-- Chrome.exe Path geçersiz. Chrome uygulamasının kurulu olduğu klasörde, exe dosyasını da içerecek şekilde Path giriniz.");
            }

            if (string.IsNullOrEmpty(txtOtpKey.Text))
            {
                errMessage.AppendLine("-- OTP Key boş değer olamaz !!! VPN sistemine tarayıcı üzerinden kendiniz girerek, Token QR Code üzerinden key değerini alınız. QR Code okumak için örnek web sitesi : zxing.org/w/decode.jspx");
            }


            if (errMessage.Length > 0)
            {
                throw new ArgumentException(errMessage.ToString());
            }
        }

        private void InitializeChromeDriver(string chromeLocation, string chromeDataDir, bool showBrowser)
        {
            if (ChromeDriver != null) return;
            var service = ChromeDriverService.CreateDefaultService();
            service.HideCommandPromptWindow = true;
            var options = CreateDriverOptions(chromeLocation, chromeDataDir, showBrowser);
            ChromeDriver = new ChromeDriver(service, options);
            if (showBrowser)
            {
                ChromeDriver.Manage().Window.Maximize();
            }
            else
            {
                ChromeDriver.Manage().Window.Size = new Size(800, 800);
                ChromeDriver.Manage().Window.Position = new Point(6000, 6000);
            }
        }

        private static ChromeOptions CreateDriverOptions(string chromeLocation, string chromeDataDir, bool showBrowser)
        {
            var options = new ChromeOptions()
            {
                AcceptInsecureCertificates = true,
                BinaryLocation = chromeLocation,
            };
            options.AddArgument("--disable-user-media-security");
            options.AddArgument("--disable-web-security");
            options.AddArgument("--allow-running-insecure-content");
            options.AddArgument("--disable-popup-blocking");
            options.AddArgument("--disable-notifications");
            options.AddArgument($"--user-data-dir={chromeDataDir}");
            options.AddArgument(!showBrowser ? "--window-size=800,800" : "--window-size=1200,800");
            return options;
        }

        private async Task StartVpnConnection()
        {
            var btnVpn = await FindElementByIdUntilAppears("/VPN/vpn_policy_na_res", 10000);
            btnVpn.Click();
        }

        private async Task SubmitOtpCode(string code)
        {
            var txtOtp = await FindElementByNameUntilAppears("password_token", 5000);
            txtOtp.Click();
            txtOtp.SendKeys(code);
            var btnSubmitOtp = await FindElementByClassNameUntilAppears("credentials_input_submit", 1000);
            btnSubmitOtp.Submit();
        }

        private static string GenerateOtpCode(string otpSecret)
        {
            var timestamp = Convert.ToInt64(Convert.ToInt64(Math.Round((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds)) / 30);
            var data = BitConverter.GetBytes(timestamp).Reverse().ToArray();
            var hmac = new HMACSHA1(ToBytes(otpSecret)).ComputeHash(data);
            var offset = hmac.Last() & 0x0F;
            var oneTimePassword = (
                                      ((hmac[offset + 0] & 0x7f) << 24) |
                                      ((hmac[offset + 1] & 0xff) << 16) |
                                      ((hmac[offset + 2] & 0xff) << 8) |
                                      (hmac[offset + 3] & 0xff)) % 1000000;

            var code = oneTimePassword.ToString().PadLeft(6, '0');
            return code;
        }

        private async Task ChooseOtpOption()
        {
            var linkOtp = await FindElementByLinkTextUntilAppears("Token", 2000);
            linkOtp.Click();
        }

        private async Task LoginSession(string f5UserName, string f5PasswordHash)
        {
            var inpUserName = await FindElementByNameUntilAppears("username", 30000);
            if (inpUserName == null)
            {
                throw new InvalidOperationException("Kullanıcı giriş sayfasına erişilemedi. VPN uygulamasının mevcutta açık olup olmadığını kontrol ediniz.");
            }
            var inpPassword = await FindElementByNameUntilAppears("password", 1000);
            inpUserName.Click();
            inpUserName.SendKeys(f5UserName);
            inpPassword.Click();
            inpPassword.SendKeys(f5PasswordHash);
            var btnSubmit = await FindElementByClassNameUntilAppears("credentials_input_submit", 1000);
            btnSubmit.Click();
        }

        private async Task NewSessionIfNecessary()
        {
            var linkToNewSession = await FindElementByLinkTextUntilAppears("tıklayınız", 2000);
            if (linkToNewSession != null)
            {
                linkToNewSession.Click();
                await NewSessionIfNecessary();
            }
            var linkToNewSession2 = await FindElementByLinkTextUntilAppears("click here.", 300);
            if (linkToNewSession2 != null)
            {
                linkToNewSession2.Click();
                await NewSessionIfNecessary();
            }
            var linkToNewSession3 = await FindElementByLinkTextUntilAppears("Skip Endpoint inspection", 300);
            if (linkToNewSession3 != null)
            {
                linkToNewSession3.Click();
                await NewSessionIfNecessary();
            }
            var linkToNewSession4 = await FindElementByLinkTextUntilAppears("Start a new session", 300);
            if (linkToNewSession4 != null)
            {
                linkToNewSession4.Click();
                await NewSessionIfNecessary();
            }

        }

        private static string CopyChromeDataDirToTemp(string chromeBaseDataDir)
        {

            var tempDataDir = Path.Combine(Path.GetTempPath(), "f5-login-chrome-data");

            if (!Directory.Exists(tempDataDir))
            {
                Directory.CreateDirectory(tempDataDir);
                CopyDirectory(chromeBaseDataDir, tempDataDir);
            }


            return tempDataDir;
        }

        private static void CopyDirectory(string sourcePath, string destinationPath)
        {
            //Now Create all of the directories
            foreach (var dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, destinationPath));
            }

            //Copy all the files & Replaces any files with the same name
            foreach (var newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                ForceCopyFile(newPath, newPath.Replace(sourcePath, destinationPath));
            }
        }

        private static void ForceCopyFile(string sourcePath, string destinationPath)
        {
            try
            {
                File.Copy(sourcePath, destinationPath, true);
            }
            catch (IOException e)
            {
                try
                {
                    if (e.Message.Contains("in use"))
                    {
                        var process = new Process
                        {
                            StartInfo =
                            {
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                FileName = "cmd.exe",
                                Arguments = $"/C copy \"{sourcePath}\" \"{destinationPath}\""
                            }
                        };
                        process.Start();
                        process.WaitForExit();
                        process.Close();
                    }
                }
                catch
                {
                    // do nothing.
                }
            }
        }

        public static byte[] ToBytes(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                throw new ArgumentNullException(nameof(input));
            }

            input = input.TrimEnd('='); // remove padding characters
            var byteCount = input.Length * 5 / 8; // this must be TRUNCATED
            var returnArray = new byte[byteCount];

            byte curByte = 0, bitsRemaining = 8;
            var arrayIndex = 0;

            foreach (var c in input)
            {
                var cValue = CharToValue(c);

                int mask;
                if (bitsRemaining > 5)
                {
                    mask = cValue << (bitsRemaining - 5);
                    curByte = (byte)(curByte | mask);
                    bitsRemaining -= 5;
                }
                else
                {
                    mask = cValue >> (5 - bitsRemaining);
                    curByte = (byte)(curByte | mask);
                    returnArray[arrayIndex++] = curByte;
                    curByte = (byte)(cValue << (3 + bitsRemaining));
                    bitsRemaining += 3;
                }
            }

            // if we didn't end with a full byte
            if (arrayIndex != byteCount)
            {
                returnArray[arrayIndex] = curByte;
            }

            return returnArray;
        }

        private static int CharToValue(char c)
        {
            var value = (int)c;

            // 65-90 == uppercase letters
            if (value < 91 && value > 64)
            {
                return value - 65;
            }

            // 50-55 == numbers 2-7
            if (value < 56 && value > 49)
            {
                return value - 24;
            }

            // 97-122 == lowercase letters
            if (value < 123 && value > 96)
            {
                return value - 97;
            }

            throw new ArgumentException("hmmmmmmmmmm", nameof(c));
        }

        private async Task<IWebElement> FindElementByLinkTextUntilAppears(string linkText, int maxTimeout)
        {
            return await FindElementBySomething(ChromeDriver.FindElementByLinkText, linkText, maxTimeout);
        }

        private async Task<IWebElement> FindElementByNameUntilAppears(string elmName, int maxTimeout)
        {
            return await FindElementBySomething(ChromeDriver.FindElementByName, elmName, maxTimeout);
        }

        private async Task<IWebElement> FindElementByClassNameUntilAppears(string className, int maxTimeout)
        {
            return await FindElementBySomething(ChromeDriver.FindElementByClassName, className, maxTimeout);
        }

        private async Task<IWebElement> FindElementByIdUntilAppears(string className, int maxTimeout)
        {
            return await FindElementBySomething(ChromeDriver.FindElementById, className, maxTimeout);
        }

        private async Task<IWebElement> FindElementBySomething(Func<string, IWebElement> findMethod, string findText, int maxTimeout)
        {
            IWebElement result = null;
            var startTime = DateTime.Now;
            while (result == null)
            {
                if ((DateTime.Now - startTime).TotalMilliseconds > maxTimeout)
                {
                    return null;
                }
                Thread.Sleep(250);
                try
                {
                    result = findMethod(findText);
                }
                catch (NoSuchElementException)
                {
                    // try again until element shows up
                    // continue;
                }

            }
            return result;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var timer = new Timer { Interval = 500 };
            timer.Elapsed += (obj, evn) =>
            {
                var validateSuccess = false;
                btnLogin.Invoke(new Action(() =>
                {
                    timer.Stop();
                    try
                    {
                        ValidateInputs();
                        validateSuccess = true;
                    }
                    catch
                    {
                        // ignore
                    }
                }));
                if (validateSuccess)
                {
                    Task.Run(ConnectAsync);
                }
            };
            timer.Start();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            CloseChromeDriver();
        }

        public void CloseChromeDriver()
        {
            if (ChromeDriver != null)
            {
                try
                {
                    ChromeDriver.Close();
                }
                catch
                {
                    // ignored
                }

                try
                {
                    ChromeDriver.Dispose();
                }
                catch
                {
                    // ignored
                }

                ChromeDriver = null;
            }
        }

        private void LinkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            MessageBox.Show("OneClickF5VPN v1.0\r\n\r\nDeveloped by İbrahim Aydın\r\n\r\nibrahim.aydin15@saglik.gov.tr");
        }
    }
}
