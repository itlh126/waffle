using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Waffle.Windows.AuthProvider;
using System.ComponentModel;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Threading;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Configuration;

namespace Waffle.Windows.AuthProvider.UnitTests
{
    [TestFixture]
    public class WindowsAuthProviderImplUnitTests
    {

        class UserTestData
        {
            public string Username;
            public string Fqn;
            public string Password;

            public UserTestData(string username, string fqn)
                : this(username, fqn, string.Empty)
            {
            }

            public UserTestData(string username, string fqn, string password)
            {
                Username = username;
                Fqn = fqn;
                Password = password;
            }
        }

        [Test, ExpectedException(typeof(Win32Exception), ExpectedMessage = "Logon failure: unknown user name or bad password")]
        public void TestLogonUser()
        {
            WindowsAuthProviderImpl windowsAuthProviderImpl = new WindowsAuthProviderImpl();
            windowsAuthProviderImpl.LogonUser("Administrator", "invalid password");
        }

        [Test, ExpectedException(typeof(Win32Exception), ExpectedMessage = "Logon failure: unknown user name or bad password")]
        public void TestLogonUserWithDomain()
        {
            WindowsAuthProviderImpl windowsAuthProviderImpl = new WindowsAuthProviderImpl();
            windowsAuthProviderImpl.LogonDomainUser("Administrator", "domain", "invalid password");
        }

        [Test, ExpectedException(typeof(Win32Exception), ExpectedMessage = "Logon failure: unknown user name or bad password")]
        public void TestLogonUserWithAllOptions()
        {
            WindowsAuthProviderImpl windowsAuthProviderImpl = new WindowsAuthProviderImpl();
            windowsAuthProviderImpl.LogonDomainUserEx("Administrator", "domain", "invalid password",
                Advapi32.LogonType.LOGON32_LOGON_NETWORK, Advapi32.LogonProvider.LOGON32_PROVIDER_DEFAULT);
        }

        [Test]
        public void TestLookupAccount()
        {
            string testDomainDNS = ConfigurationSettings.AppSettings["testDomainDNS"];
            string testDomainNETBIOS = ConfigurationSettings.AppSettings["testDomainNETBIOS"];
            string testUsername = ConfigurationSettings.AppSettings["testUsername"];
            string testPassword = ConfigurationSettings.AppSettings["testPassword"];

            string testDomainUsernameNETBIOS = string.Format(@"{0}\{1}", testDomainNETBIOS, testUsername);
            string testDomainUsernameDNS = string.Format(@"{0}\{1}", testDomainDNS, testUsername);
            string testUsernameAtDomainNETBIOS = string.Format(@"{1}@{0}", testDomainNETBIOS, testUsername);

            UserTestData[] testData = 
            {
                new UserTestData(testDomainUsernameNETBIOS.ToLower(), testDomainUsernameNETBIOS),
                new UserTestData(testDomainUsernameDNS, testDomainUsernameNETBIOS),
                new UserTestData(testUsernameAtDomainNETBIOS, testDomainUsernameNETBIOS, testPassword),
                new UserTestData(@"Administrator", string.Format(@"{0}\Administrator", 
                    Environment.GetEnvironmentVariable("COMPUTERNAME").ToLower()))
            };

            WindowsAuthProviderImpl windowsAuthProviderImpl = new WindowsAuthProviderImpl();
            foreach (UserTestData test in testData)
            {
                IWindowsAccount account = windowsAuthProviderImpl.LookupAccount(test.Username);
                Console.WriteLine(account.SidString);
                Console.WriteLine(account.Fqn);
                Assert.AreEqual(test.Fqn.ToLower(), account.Fqn.ToLower());
            }
        }

        [Test]
        public void TestLogonRealUser()
        {
            string testDomainDNS = ConfigurationSettings.AppSettings["testDomainDNS"];
            string testDomainNETBIOS = ConfigurationSettings.AppSettings["testDomainNETBIOS"];
            string testUsername = ConfigurationSettings.AppSettings["testUsername"];
            string testPassword = ConfigurationSettings.AppSettings["testPassword"];

            string testDomainUsernameNETBIOS = string.Format(@"{0}\{1}", testDomainNETBIOS, testUsername);
            string testDomainUsernameDNS = string.Format(@"{0}\{1}", testDomainDNS, testUsername);
            string testUsernameAtDomainNETBIOS = string.Format(@"{1}@{0}", testDomainNETBIOS, testUsername);

            UserTestData[] testData = 
            {
                new UserTestData(testDomainUsernameNETBIOS.ToLower(), testDomainUsernameNETBIOS, testPassword),
                new UserTestData(testDomainUsernameDNS, testDomainUsernameNETBIOS, testPassword),
                new UserTestData(testUsernameAtDomainNETBIOS, testDomainUsernameNETBIOS, testPassword),                
                //new UserTestData(@"Administrator", string.Format(@"{0}\Administrator", 
                //    Environment.GetEnvironmentVariable("COMPUTERNAME").ToLower()), "admin123")
            };

            WindowsAuthProviderImpl windowsAuthProviderImpl = new WindowsAuthProviderImpl();
            foreach (UserTestData test in testData)
            {
                IWindowsIdentity account = windowsAuthProviderImpl.LogonUser(test.Username, test.Password);
                Console.WriteLine(account.SidString);
                Console.WriteLine(account.Fqn);
                Assert.AreEqual(test.Fqn.ToLower(), account.Fqn.ToLower());
                Assert.IsTrue(Advapi32.IsValidSid(account.Sid));

                foreach (string groupName in account.Groups)
                {
                    Console.WriteLine("Group: {0}", groupName);
                }
            }
        }

        [Test]
        public void TestGetDomains()
        {
            WindowsAuthProviderImpl windowsAuthProviderImpl = new WindowsAuthProviderImpl();
            IWindowsComputer computer = windowsAuthProviderImpl.GetCurrentComputer();
            Console.WriteLine("{0}, {1} ({2})", computer.ComputerName, computer.MemberOf, computer.JoinStatus);

            if (computer.JoinStatus != Netapi32.NetJoinStatus.NetSetupDomainName.ToString())
                Assert.Ignore(string.Format("{0} is not joined to a domain.", computer.ComputerName));

            IWindowsDomain[] domains = windowsAuthProviderImpl.GetDomains();
            Assert.IsNotNull(domains);
            Assert.IsTrue(domains.Length > 0);
            foreach (IWindowsDomain domain in domains)
            {
                Console.WriteLine("{0} ({1}, {2})", domain.Fqn,
                    domain.TrustDirectionString, domain.TrustTypeString);
                try
                {
                    Console.WriteLine(" Canonical name: {0}", domain.CanonicalName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(" Error obtaining domain canonical name: {0}", ex.Message);
                }

                try
                {
                    foreach (string group in domain.Groups)
                    {
                        Console.WriteLine(" Group: {0}", group);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(" Error enumerating groups: {0}", ex.Message);
                }
            }
        }

        [Test]
        public void TestImpersonation()
        {
            string testDomainDNS = ConfigurationSettings.AppSettings["testDomainDNS"];
            string testDomainNETBIOS = ConfigurationSettings.AppSettings["testDomainNETBIOS"];
            string testUsername = ConfigurationSettings.AppSettings["testUsername"];
            string testPassword = ConfigurationSettings.AppSettings["testPassword"];

            string testDomainUsernameNETBIOS = string.Format(@"{0}\{1}", testDomainNETBIOS, testUsername);
            string testDomainUsernameDNS = string.Format(@"{0}\{1}", testDomainDNS, testUsername);
            string testUsernameAtDomainNETBIOS = string.Format(@"{1}@{0}", testDomainNETBIOS, testUsername);

            UserTestData[] testData = 
            {
                new UserTestData(testDomainUsernameNETBIOS.ToLower(), testDomainUsernameNETBIOS, testPassword),
                new UserTestData(testDomainUsernameDNS, testDomainUsernameNETBIOS, testPassword),
                new UserTestData(testUsernameAtDomainNETBIOS, testDomainUsernameNETBIOS, testPassword),                
                // new UserTestData(@"dbp_service_user@qany.prv", @"qany\dbp_service_user", "Admin123"),
            };

            WindowsAuthProviderImpl windowsAuthProviderImpl = new WindowsAuthProviderImpl();
            IWindowsComputer computer = windowsAuthProviderImpl.GetCurrentComputer();
            Console.WriteLine("{0}, {1} ({2})", computer.ComputerName, computer.MemberOf, computer.JoinStatus);

            if (computer.JoinStatus != Netapi32.NetJoinStatus.NetSetupDomainName.ToString())
                Assert.Ignore(string.Format("{0} is not joined to a domain.", computer.ComputerName));

            foreach (UserTestData test in testData)
            {
                IWindowsIdentity account = windowsAuthProviderImpl.LogonUser(test.Username, test.Password);
                IWindowsImpersonationContext impersonationCtx = account.Impersonate();
                try
                {
                    Console.WriteLine(account.SidString);
                    Console.WriteLine(account.Fqn);
                    Assert.AreEqual(account.Fqn, WindowsIdentity.GetCurrent().Name);
                }
                finally
                {
                    impersonationCtx.RevertToSelf();
                }
            }
        }

        [Test]
        public void TestGetLocalGroups()
        {
            WindowsAuthProviderImpl windowsAuthProviderImpl = new WindowsAuthProviderImpl();
            IWindowsComputer computer = windowsAuthProviderImpl.GetCurrentComputer();
            Console.WriteLine("{0}, {1} ({2})", computer.ComputerName, computer.MemberOf, computer.JoinStatus);
            Assert.IsTrue(computer.Groups.Length > 0);
            foreach (string group in computer.Groups)
            {
                Console.WriteLine(" Group: {0}", group);
            }
        }

        [Test]
        public void TestGetDomain()
        {
            WindowsAuthProviderImpl windowsAuthProviderImpl = new WindowsAuthProviderImpl();
            IWindowsComputer computer = windowsAuthProviderImpl.GetCurrentComputer();
            Console.WriteLine("{0}, {1} ({2})", computer.ComputerName, computer.MemberOf, computer.JoinStatus);
            if (computer.JoinStatus != Netapi32.NetJoinStatus.NetSetupDomainName.ToString())
                Assert.Ignore(string.Format("{0} is not joined to a domain.", computer.ComputerName));

            IWindowsDomain currentDomain = windowsAuthProviderImpl.GetDomain(
                    computer.MemberOf);
            Assert.IsNotNull(currentDomain);
            Console.WriteLine("Domain fqn: {0}", currentDomain.Fqn);
            Console.WriteLine("Domain canonical name: {0}", currentDomain.CanonicalName);
            Assert.AreEqual(computer.MemberOf, currentDomain.Fqn);
            Assert.AreEqual(Domain.GetComputerDomain().Name, currentDomain.CanonicalName);
            foreach (string group in currentDomain.Groups)
            {
                Console.WriteLine(" Group: {0}", group);
            }
        }

        [Test, ExpectedException(typeof(Win32Exception), ExpectedMessage = "The logon attempt failed")]
        public void TestSecur32ExceptionsAreWin32Exceptions()
        {
            throw new Win32Exception(Secur32.SEC_E_LOGON_DENIED);
        }

        [Test, ExpectedException(typeof(Win32Exception), ExpectedMessage = "The token supplied to the function is invalid")]
        public void TestAcceptBadSecurityToken()
        {
            WindowsAuthProviderImpl provider = new WindowsAuthProviderImpl();
            byte[] clientToken = new byte[] { 1, 2, 3 };
            IWindowsSecurityContext ctx = provider.AcceptSecurityToken(Guid.NewGuid().ToString(), clientToken, "Negotiate");
        }

        [Test]
        public void TestAcceptExistingSecurityToken()
        {
            WindowsAuthProviderImpl provider = new WindowsAuthProviderImpl();
            byte[] clientToken = System.Convert.FromBase64String("YIIFWgYGKwYBBQUCoIIFTjCCBUqgJDAiBgkqhkiC9xIBAgIGCSqGSIb3EgECAgYKKwYBBAGCNwICCqKCBSAEggUcYIIFGAYJKoZIhvcSAQICAQBuggUHMIIFA6ADAgEFoQMCAQ6iBwMFACAAAACjggQuYYIEKjCCBCagAwIBBaEPGw1OWUNBUFQzNUsuQ09NoikwJ6ADAgECoSAwHhsESFRUUBsWamhpdGUyazMubnljYXB0MzVrLmNvbaOCA+EwggPdoAMCARehAwIBBaKCA88EggPL9ISsFstUd6KU8hUwiXh5axPlaoLgDfHF0jxsRiLVbKdB8WFSDbbICl3reAR2fPF8QVTL8KU2dZYuX770PFXZEBR/ZhyOfIUMtX2mmBwWZDDPU5MvMCbQcEhRSlweO/UQoAVzsRjq/wBrIKFEHpWm1LVE9Mqg/4rLmTTkdCIklB/HSHGz2Q64F37k3Q+NfM7T7wIMOsRJa1IIJt/2tpGwbYM6MJOPBVNw7UggFrJmdcm94TDgRZhBoC3E2UmJnt8GmlryPbE7i5WWoO24mx5VC43p6+WZYPhgyEwHzfrycar0BLNvJC1Tc8j2CDmAybUUczPCUhBIjYWGNuY/ILoZZTr1aTM4Y4tNJu/rpoe7SDtqWNWtGcdl8dDLTJBkbooymMYZuTCU6H5MWh7zvHbF7v3XjZtNPyhEiS0hVQ8y4hyeeAP5T1LdW5NVClHYx3HjZt8upcCYqczRGHYix9y/Fkq3xF70O3F0SoJl+DNsNI5D/otE4rOl472Jz8ywQ0hx+zSlIzmtAF0E2w47vFF7w64yYOf5TFUaOPtuvBVxr3EZUaOsN7+9AMaSY9C8LXVELfdVJBc12sX8NW03L/LywDk6svlcfebqxkQzdeJwk31H8KsX0oeFWgXCVI/pTHKD+JdrO8pjr8pMP1hN/95xHoYRqG4OIXVwi5pxjhf6r0Ir32791zKdUjfaO2CVZSyvxA/suE0Ew+Xyy5MVXcS9l2MKfRycAbSdHKIrqg14/njUwoldmnrVE3zZamQb6sWo+wjMv1EPj8c5bCLjURUHdwOFqS+5DF4O4m2sX+NKcL0s96fTWQHaQr+5LlSO1E8kWJ+d1M0oRsYzzD+iUkPzkoOrFRbtGZ+dokGhy2jp9iu2jd2P8I+ZLPHc8lpwqbKMcHllEQj7XcCrMEXAt18BhvlM4TdpMr3totyb4wVoSIiTHnyVgt7tG3ub/1oJ2sp+IrdvsGx//y0ANyHuEfk0kjG/iBdcKvs75yEHCpe40Ze91AtjAWYT8rbXvrIwPS24NX+ukoTOhvZY8S6F/18Yp2fv0/MTL32FuoPQ8tqJu8NMWFGACu+d3jAcZerh6k18pCCW6iw0ji/Ijtc3ok9lyZrJNmwDFHRbNOOPdHE5pF5vOHezJ+PUStzkHovtydLc7IGPswYfwrM8T+8Fx+T7zUkoLAoJIN2c4IMKKx6/XYvY3hW7sV1LaietDXJJMPeBvxur+CDykHyDeFQ1JESJv+KVdL7c7uxEgkD4GYloecy3HqMfclUDrevVrq2L6USSeCeYWJDs6rEe1mKkgbswgbigAwIBF6KBsASBrT/Bj9kDPy4erA5ZeCnJmMLY8l3d1WuvZ2E602uS1eQcIPHVzZNJVjq0HaY7BGtL4WcHCRhLUittEX10BHV5NhOApyY7NPQXxekjvoei6Dy5eM47neqYXC7ljtc3vRI+LNqywIHDnja3YJC1vZOhEpk9x0BFhqqFhrGjaLyPvGHftIX8sHpV5UH27kmQYaVlykRK7Rp0kbywCccdK8dI3Ax3B7i1jckjx18a13f/");
            IWindowsSecurityContext ctx = provider.AcceptSecurityToken(Guid.NewGuid().ToString(), clientToken, "Negotiate");
            Assert.IsTrue(ctx.Continue);
        }

        [Test]
        public void TestAcceptSecurityToken()
        {
            string[] securityPackages = { "Negotiate", "NTLM" };

            foreach (string package in securityPackages)
            {
                Console.WriteLine("-- Security package: {0}", package);
                WindowsAuthProviderImpl provider = new WindowsAuthProviderImpl();

                WindowsSecurityContext initContext = WindowsSecurityContext.GetCurrent(package);
                IWindowsSecurityContext continueContext = initContext;
                IWindowsSecurityContext responseContext = null;
                string connectionId = Guid.NewGuid().ToString();
                do
                {
                    responseContext = provider.AcceptSecurityToken(connectionId, continueContext.Token, package);
                    if (responseContext.Token != null)
                    {
                        Console.WriteLine("  Token: {0}", Convert.ToBase64String(responseContext.Token));
                        Console.WriteLine("  Continue: {0}", responseContext.Continue);
                    }
                    continueContext = new WindowsSecurityContext(initContext, responseContext.Token);
                } while (responseContext.Continue);

                Assert.IsFalse(responseContext.Continue);
                Console.WriteLine(responseContext.Identity.Fqn);
            }
        }
    }
}
