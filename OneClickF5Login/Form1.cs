using System;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OneClickF5Login.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using Timer = System.Timers.Timer;


namespace OneClickF5Login
{
    public partial class Form1 : Form
    {
        private readonly string _defaultBrowserDataPath = @"firefox-browser\profile-data";

        private readonly string _defaultBrowserPath = @"firefox-browser\firefox.exe";
        private FirefoxDriver BrowserDriver { get; set; }

        private const string ConfigUid = "{049DD0E0-1751-45B3-B840-84ED1C6A018A}";

        private static string _statusMessage = string.Empty;

        public Form1()
        {
            var mainDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
            mainDir = mainDir ?? throw new InvalidOperationException();
            _defaultBrowserPath = Path.Combine(mainDir, _defaultBrowserPath);
            _defaultBrowserDataPath = Path.Combine(mainDir, _defaultBrowserDataPath);
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
            txtBrowserDataPath.Text = string.IsNullOrEmpty(coll?.BrowserDataPath) ? _defaultBrowserDataPath : coll.BrowserDataPath;
            txtBrowserPath.Text = string.IsNullOrEmpty(coll?.BrowserPath) ? _defaultBrowserPath : coll.BrowserPath;
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
                _statusMessage = "Browser sürücüsü yükleniyor.";
                InitializeBrowserDriver(txtBrowserPath.Text, txtBrowserDataPath.Text, chkShowBrowser.Checked);
                if (chkShowBrowser.Checked)
                {
                    BrowserDriver.Manage().Window.Maximize();
                }
                else
                {
                    BrowserDriver.Manage().Window.Size = new Size(800, 800);
                    BrowserDriver.Manage().Window.Position = new Point(6000, 6000);
                }
                _statusMessage = "Browser ayarları yapılıyor.";
                Thread.Sleep(500);
                var cookies = BrowserDriver.Manage().Cookies.AllCookies.Where(i => !string.IsNullOrEmpty(i.Domain) && i.Domain.Contains("sslvpn.saglik.gov.tr")).ToList();
                cookies.ForEach(cookie => BrowserDriver.Manage().Cookies.DeleteCookie(cookie));
                _statusMessage = "Sağlık Bakanlığı SSL VPN sayfası yükleniyor.";
                BrowserDriver.Url = "https://sslvpn.saglik.gov.tr";
                Thread.Sleep(500);
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
                Thread.Sleep(500);
                _statusMessage = "";
                EnableDisableForm(true);
                btnLogin.Invoke(new Action(Close));
            }
            catch (Exception exception)
            {
                _statusMessage = "";
                EnableDisableForm(true);
                btnLogin.Invoke(new Action(() =>
                {
                    MessageBox.Show($"{exception.Message}\r\n--------------\r\n--------------\r\n{exception}");
                }));
            }
        }

        private void EnableDisableForm(bool enabled)
        {
            btnLogin.Invoke(new Action(() =>
            {
                btnLogin.Enabled = enabled;
                txtUserName.Enabled = enabled;
                txtBrowserDataPath.Enabled = enabled;
                txtBrowserPath.Enabled = enabled;
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
                Thread.Sleep(200);
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
                BrowserDataPath = txtBrowserDataPath.Text,
                BrowserPath = txtBrowserPath.Text,
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

            if (string.IsNullOrEmpty(txtBrowserDataPath.Text))
            {
                errMessage.AppendLine("-- Browser Data Path boş değer olamaz !!!");
            }
            else if (!Directory.Exists(txtBrowserDataPath.Text))
            {
                errMessage.AppendLine($"-- Browser Data Path geçersiz. Browser uygulamasının kullanıcı profil ve ayarlarını sakladığı root klasörü girmelisiniz. Örnek : {_defaultBrowserDataPath}");
            }

            if (string.IsNullOrEmpty(txtBrowserPath.Text))
            {
                errMessage.AppendLine("-- Browser.exe Path boş değer olamaz !!!");
            }
            else if (!File.Exists(txtBrowserPath.Text))
            {
                errMessage.AppendLine("-- Browser.exe Path geçersiz. Browser uygulamasının kurulu olduğu klasörde, exe dosyasını da içerecek şekilde Path giriniz.");
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

        private void InitializeBrowserDriver(string browserLocation, string browserDataDir, bool showBrowser)
        {
            if (BrowserDriver != null) return;
            var service = FirefoxDriverService.CreateDefaultService();
            service.HideCommandPromptWindow = true;
            var options = CreateDriverOptions(browserLocation, browserDataDir, showBrowser);
            BrowserDriver = new FirefoxDriver(service, options);
            if (showBrowser)
            {
                BrowserDriver.Manage().Window.Maximize();
            }
            else
            {
                BrowserDriver.Manage().Window.Size = new Size(800, 800);
                BrowserDriver.Manage().Window.Position = new Point(6000, 6000);
            }
        }

        private static FirefoxOptions CreateDriverOptions(string browserLocation, string browserDataDir, bool showBrowser)
        {
            var profile = new FirefoxProfile(browserDataDir);
            //profile.SetPreference("dom.disable_open_during_load", false);
            var options = new FirefoxOptions()
            {
                AcceptInsecureCertificates = true,
                BrowserExecutableLocation = browserLocation,
                Profile = profile,
                UnhandledPromptBehavior = UnhandledPromptBehavior.Accept
            };
            // options.AddArgument($"-profile \"{browserDataDir}\"");
            //options.AddArgument(!showBrowser ? "-width 200" : "-width 200");
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
            return await FindElementBySomething(BrowserDriver.FindElementByLinkText, linkText, maxTimeout);
        }

        private async Task<IWebElement> FindElementByNameUntilAppears(string elmName, int maxTimeout)
        {
            return await FindElementBySomething(BrowserDriver.FindElementByName, elmName, maxTimeout);
        }

        private async Task<IWebElement> FindElementByClassNameUntilAppears(string className, int maxTimeout)
        {
            return await FindElementBySomething(BrowserDriver.FindElementByClassName, className, maxTimeout);
        }

        private async Task<IWebElement> FindElementByIdUntilAppears(string className, int maxTimeout)
        {
            return await FindElementBySomething(BrowserDriver.FindElementById, className, maxTimeout);
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
            CloseBrowserDriver();
        }

        public void CloseBrowserDriver()
        {
            if (BrowserDriver != null)
            {
                try
                {
                    BrowserDriver.Close();
                }
                catch
                {
                    // ignored
                }

                try
                {
                    BrowserDriver.Dispose();
                }
                catch
                {
                    // ignored
                }

                BrowserDriver = null;
            }
        }

        private void LinkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            MessageBox.Show("OneClickF5VPN v1.0\r\n\r\nDeveloped by İbrahim Aydın\r\n\r\nibrahim.aydin15@saglik.gov.tr");
        }
    }
}
