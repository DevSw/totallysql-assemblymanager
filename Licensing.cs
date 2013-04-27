using System;
using System.Text;

using InteractiveStudios.QlmLicenseLib;


namespace QLM
{
    public class LicenseValidator
    {

        private QlmLicense license = new QlmLicense();
        private string activationKey;
        private string computerKey;

        private bool isEvaluation = false;
        private bool evaluationExpired = true;
        private int evaluationRemainingDays = -1;

        /// <summary>
        /// Constructor initializes the license product definition
        /// </summary>
        public LicenseValidator()
        {
            license = new QlmLicense();

            // Always obfuscate your code. In particular, you should always obfuscate all arguments
            // of DefineProduct and the Public Key (i.e. encrypt all the string arguments).

            license.DefineProduct(1, "Assembly Manager", 1, 0, "DemoKey", "{24EAA3C1-3DD7-40E0-AEA3-D20AA17A6005}");
            license.PublicKey = "AbHDdGw6zCVDBA==";

            // If you are using QLM Pro, you should also set the communicationEncryptionKey property
            // The CommunicationEncryptionKey must match the value specified in the web.config file of the QLM web service
            //license.CommunicationEncryptionKey = "{B6163D99-F46A-4580-BB42-BF276A507A14}";
        }

        /// <remarks>Call ValidateLicenseAtStartup when your application is launched. 
        /// If this function returns false, exit your application.
        /// </remarks>
        /// 
        /// <summary>
        /// Validates the license when the application starts up. 
        /// The first time a license key is validated successfully,
        /// it is stored in a hidden file on the system. 
        /// When the application is restarted, this code will load the license
        /// key from the hidden file and attempt to validate it again. 
        /// If it validates succesfully, the function returns true.
        /// If the license key is invalid, expired, etc, the function returns false.
        /// </summary>
        /// <param name="computerID">Unique Computer identifier</param>
        /// <param name="returnMsg">Error message returned, in case of an error</param>
        /// <returns>true if the license is OK.</returns>
        public bool ValidateLicenseAtStartup(string computerID, ref bool needsActivation, ref string returnMsg)
        {
            returnMsg = string.Empty;
            needsActivation = false;

            string storedActivationKey = string.Empty;
            string storedComputerKey = string.Empty;

            license.ReadKeys(ref storedActivationKey, ref storedComputerKey);

            if (!String.IsNullOrEmpty(storedActivationKey))
            {
                activationKey = storedActivationKey;
            }

            if (!String.IsNullOrEmpty(storedComputerKey))
            {
                computerKey = storedComputerKey;
            }

            return ValidateLicense(activationKey, computerKey, computerID, ref needsActivation, ref returnMsg);

        }

        /// <remarks>Call this function in the dialog where the user enters the license key to validate the license.</remarks>
        /// <summary>
        /// Validates a license key. If you provide a computer key, the computer key is validated. 
        /// Otherwise, the activation key is validated. 
        /// If you are using machine bound keys (UserDefined), you can provide the computer identifier, 
        /// otherwise set the computerID to an empty string.
        /// </summary>
        /// <param name="activationKey">Activation Key</param>
        /// <param name="computerKey">Computer Key</param>
        /// <param name="computerID">Unique Computer identifier</param>
        /// <returns>true if the license is OK.</returns>
        public bool ValidateLicense(string activationKey, string computerKey, string computerID, ref bool needsActivation, ref string returnMsg)
        {
            bool ret = false;
            needsActivation = false;

            string licenseKey = computerKey;

            if (String.IsNullOrEmpty(licenseKey))
            {
                licenseKey = activationKey;

                if (String.IsNullOrEmpty(licenseKey))
                {
                    return false;
                }
            }

            returnMsg = license.ValidateLicenseEx(licenseKey, computerID);

            int nStatus = (int)license.GetStatus();

            if (IsTrue(nStatus, (int)ELicenseStatus.EKeyInvalid) ||
                IsTrue(nStatus, (int)ELicenseStatus.EKeyProductInvalid) ||
                IsTrue(nStatus, (int)ELicenseStatus.EKeyVersionInvalid) ||
                IsTrue(nStatus, (int)ELicenseStatus.EKeyMachineInvalid) ||
                IsTrue(nStatus, (int)ELicenseStatus.EKeyTampered))
            {
                // the key is invalid
                ret = false;
            }
            else if (IsTrue(nStatus, (int)ELicenseStatus.EKeyDemo))
            {
                isEvaluation = true;

                if (IsTrue(nStatus, (int)ELicenseStatus.EKeyExpired))
                {
                    // the key has expired
                    ret = false;
                    evaluationExpired = true;
                }
                else
                {
                    // the demo key is still valid
                    ret = true;
                    evaluationRemainingDays = license.DaysLeft;
                }
            }
            else if (IsTrue(nStatus, (int)ELicenseStatus.EKeyPermanent))
            {
                // the key is OK                
                ret = true;
            }

            if (ret == true)
            {

                if (license.LicenseType == ELicenseType.Activation)
                {
                    needsActivation = true;
                    ret = false;
                }
                else
                {
                    license.StoreKeys(activationKey, computerKey);
                }
            }

            return ret;

        }

        /// <summary>
        /// Deletes the license keys stored on the computer. 
        /// </summary>
        public void DeleteKeys()
        {
            license.DeleteKeys();
        }

        /// <summary>
        /// Returns the registered activation key
        /// </summary>
        public string ActivationKey
        {
            get
            {
                return activationKey;
            }
        }

        /// <summary>
        /// Returns the registered computer key
        /// </summary>
        public string ComputerKey
        {
            get
            {
                return computerKey;
            }
        }

        public bool IsEvaluation
        {
            get
            {
                return isEvaluation;
            }
        }

        public bool EvaluationExpired
        {
            get
            {
                return evaluationExpired;
            }
        }

        public int EvaluationRemainingDays
        {
            get
            {
                return evaluationRemainingDays;
            }
        }

        /// <summary>
        /// Returns the underlying license object
        /// </summary>
        public QlmLicense QlmLicense
        {
            get
            {
                return license;
            }
        }

        /// <summary>
        /// Compares flags
        /// </summary>
        /// <param name="nVal1">Value 1</param>
        /// <param name="nVal2">Value 2</param>
        /// <returns></returns>
        private bool IsTrue(int nVal1, int nVal2)
        {
            if (((nVal1 & nVal2) == nVal1) || ((nVal1 & nVal2) == nVal2))
            {
                return true;
            }
            return false;
        }

    }
}