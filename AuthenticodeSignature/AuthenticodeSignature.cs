// Copyright (c) 2024 Roger Brown.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;

namespace RhubarbGeekNz.AuthenticodeSignature
{
    public abstract class AuthenticodeSignaturePSCmdlet : PSCmdlet, IDisposable
    {
        private WebClient webClient;
        private string endPoint, authorization;

        protected byte[] UploadFile(IDictionary<string, string> queryParams, string filePath)
        {
            if (webClient == null)
            {
                string local = string.Join(Path.DirectorySeparatorChar.ToString(),
                    new string[] {
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "rhubarb-geek-nz.signtool",
                    "signtool.config"
                    });

                XmlDocument xmlDoc = new XmlDocument();

                xmlDoc.Load(local);

                endPoint = xmlDoc.SelectSingleNode("/SignTool/Endpoint").InnerText;
                authorization = xmlDoc.SelectSingleNode("/SignTool/Authorization").InnerText;

                webClient = new WebClient();

                webClient.Headers["Authorization"] = authorization;
            }

            string url = string.Join(
                "?",
                new string[] {
                        endPoint,
                        string.Join("&",
                            queryParams.Select(
                                (x) => string.Join(
                                    "=",
                                    new string[] {
                                        x.Key,
                                        WebUtility.UrlEncode(x.Value)
                                    }
                                )
                            )
                        )
                }
            );

            return webClient.UploadFile(url, filePath);
        }

        private static readonly IDictionary<SignatureStatus, uint> SignatureStatusToWin32Error = new Dictionary<SignatureStatus, uint>()
        {
            { SignatureStatus.Valid,0U},
            { SignatureStatus.Incompatible,0x80090008},
            { SignatureStatus.UnknownError,0xFFFFFFFF},
            { SignatureStatus.NotSupportedFileFormat,0x800b0001},
            { SignatureStatus.NotTrusted,0x800B0111},
            { SignatureStatus.HashMismatch,0x8009200d},
            { SignatureStatus.NotSigned,0x800b0100}
        };

        internal object CreateSignature(string filePath, X509Certificate2 signer, SignatureStatus status, X509Certificate2 timer)
        {
            object result = null;

            if (SignatureStatusToWin32Error.TryGetValue(status, out uint error))
            {
                Type type = typeof(Signature);

                var method = type.GetMethod("Init", BindingFlags.Instance | BindingFlags.NonPublic, new Type[]
                    {
                        typeof(string),
                        typeof(X509Certificate2),
                        typeof(uint),
                        typeof(X509Certificate2)
                    }
                );

                if (method != null)
                {
                    object signature = RuntimeHelpers.GetUninitializedObject(typeof(Signature));

                    method.Invoke(signature, new object[] { filePath, signer, error, timer });

                    if (signature is Signature sig && sig.Status == status)
                    {
                        result = signature;
                    }
                }
            }

            return result;
        }

        protected override void EndProcessing() => Dispose();

        public void Dispose()
        {
            IDisposable disposable = webClient;

            webClient = null;

            if (disposable != null)
            {
                disposable.Dispose();
            }
        }
    }

    [Cmdlet(VerbsCommon.Set, "AuthenticodeSignature")]
    public sealed class SetAuthenticodeSignature : AuthenticodeSignaturePSCmdlet
    {
        [Parameter(Mandatory = true)]
        public X509Certificate2 Certificate;
        [Parameter(Mandatory = true)]
        public string FilePath;
        [Parameter(Mandatory = true)]
        public string HashAlgorithm;
        [Parameter(Mandatory = true)]
        public string TimestampServer;

        protected override void ProcessRecord()
        {
            try
            {
                IDictionary<string, string> queryParams = new Dictionary<string, string>();

                queryParams.Add("command", "sign");
                queryParams.Add("t", TimestampServer);
                queryParams.Add("fd", HashAlgorithm);
                queryParams.Add("sha1", Certificate.Thumbprint);

                string filePath = GetUnresolvedProviderPathFromPSPath(FilePath);

                if (filePath == null)
                {
                    throw new FileNotFoundException(FilePath);
                }

                var response = UploadFile(queryParams, filePath);

                File.WriteAllBytes(filePath, response);

                object signature = CreateSignature(filePath, Certificate, SignatureStatus.Valid, null);

                if (signature == null)
                {
                    PSObject result = new PSObject();

                    result.Properties.Add(new PSNoteProperty("SignerCertificate", Certificate));
                    result.Properties.Add(new PSNoteProperty("Status", SignatureStatus.Valid));
                    result.Properties.Add(new PSNoteProperty("StatusMessage", "Signature verified."));
                    result.Properties.Add(new PSNoteProperty("Path", filePath));

                    WriteObject(result);
                }
                else
                {
                    WriteObject(signature);
                }
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, VerbsCommon.Set, ErrorCategory.NotSpecified, null));
            }
        }
    }

    [Cmdlet(VerbsCommon.Get, "AuthenticodeSignature")]
    public sealed class GetAuthenticodeSignature : AuthenticodeSignaturePSCmdlet
    {
        [Parameter(ParameterSetName = "path", Mandatory = true, Position = 0)]
        public string[] FilePath;
        [Parameter(ParameterSetName = "literal", Mandatory = true)]
        public string[] LiteralPath;

        protected override void ProcessRecord()
        {
            if (FilePath != null)
            {
                foreach (string filePath in FilePath)
                {
                    IEnumerable<string> files = null;

                    try
                    {
                        files = GetResolvedProviderPathFromPSPath(filePath, out var info);
                    }
                    catch (Exception ex)
                    {
                        WriteError(new ErrorRecord(ex, VerbsCommon.Get, ErrorCategory.ObjectNotFound, null));
                    }

                    if (files != null)
                    {
                        foreach (string file in files)
                        {
                            if (file != null)
                            {
                                if (0 == (FileAttributes.Directory & File.GetAttributes(file)))
                                {
                                    ProcessFile(file);
                                }
                            }
                        }
                    }
                }
            }

            if (LiteralPath != null)
            {
                foreach (string filePath in LiteralPath)
                {
                    string file = null;

                    try
                    {
                        file = GetUnresolvedProviderPathFromPSPath(filePath);

                        if (0 != (FileAttributes.Directory & File.GetAttributes(file)))
                        {
                            file = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        file = null;

                        WriteError(new ErrorRecord(ex, VerbsCommon.Get, ErrorCategory.ObjectNotFound, null));
                    }

                    if (file != null)
                    {
                        ProcessFile(file);
                    }
                }
            }
        }

        private void ProcessFile(string filePath)
        {
            try
            {
                IDictionary<string, string> queryParams = new Dictionary<string, string>();

                queryParams.Add("command", "verify");

                if (filePath == null)
                {
                    throw new FileNotFoundException();
                }

                var response = UploadFile(queryParams, filePath);

                using (PowerShell powerShell = PowerShell.Create(RunspaceMode.CurrentRunspace))
                {
                    powerShell.AddCommand("ConvertFrom-Json", true);

                    var output = powerShell.Invoke(new string[] { Encoding.UTF8.GetString(response) });

                    object signature = null;

                    if (output.Count == 1)
                    {
                        var outputItem = output[0];
                        X509Certificate2 signer = null, timer = null;
                        var signerValue = outputItem.Properties["SignerCertificate"];
                        var timerValue = outputItem.Properties["TimeStamperCertificate"];

                        if (signerValue != null)
                        {
                            signer = X509Certificate2.CreateFromPem(signerValue.Value.ToString());
                        }

                        if (timerValue != null)
                        {
                            timer = X509Certificate2.CreateFromPem(timerValue.Value.ToString());
                        }

                        SignatureStatus status = SignatureStatus.UnknownError;

                        var statusValue = outputItem.Properties["Status"];

                        if (statusValue != null && Enum.TryParse<SignatureStatus>(statusValue.Value.ToString(), out SignatureStatus parsedStatus))
                        {
                            status = parsedStatus;
                        }

                        signature = CreateSignature(filePath, signer, status, timer);

                        if (signature != null)
                        {
                            Type type = signature.GetType();

                            foreach (string name in new string[] { "IsOSBinary", "SignatureType" })
                            {
                                var propertyValue = outputItem.Properties[name];

                                if (propertyValue != null)
                                {
                                    var property = type.GetProperty(name);

                                    if (property != null)
                                    {
                                        Type propertyType = property.PropertyType;

                                        if (propertyType.IsEnum)
                                        {
                                            property.SetValue(signature, Enum.Parse(propertyType, propertyValue.Value.ToString()), null);
                                        }
                                        else
                                        {
                                            if (propertyType.IsAssignableFrom(propertyValue.Value.GetType()))
                                            {
                                                property.SetValue(signature, propertyValue.Value, null);
                                            }
                                        }
                                    }
                                }
                            }

                            if (status == SignatureStatus.UnknownError)
                            {
                                var propertyValue = outputItem.Properties["StatusMessage"];

                                if (propertyValue != null && !propertyValue.Value.Equals(((Signature)signature).StatusMessage))
                                {
                                    var field = type.GetField("_statusMessage", BindingFlags.NonPublic | BindingFlags.Instance);

                                    if (field != null)
                                    {
                                        field.SetValue(signature, propertyValue.Value);
                                    }
                                }
                            }
                        }
                    }

                    if (signature == null)
                    {
                        PSObject result = new PSObject();

                        foreach (var item in output)
                        {
                            foreach (var prop in item.Properties)
                            {
                                if (prop.Value != null)
                                {
                                    var info = typeof(Signature).GetProperty(prop.Name);

                                    if (info != null)
                                    {
                                        PSNoteProperty p = null;

                                        if (prop.Value is string str)
                                        {
                                            if (info.PropertyType.IsEnum)
                                            {
                                                p = new PSNoteProperty(prop.Name, Enum.Parse(info.PropertyType, str));
                                            }
                                            else
                                            {
                                                if (info.PropertyType == typeof(X509Certificate2))
                                                {
                                                    X509Certificate2 cert = X509Certificate2.CreateFromPem(str);

                                                    if (cert == null)
                                                    {
                                                        p = new PSNoteProperty(prop.Name, str);
                                                    }
                                                    else
                                                    {
                                                        p = new PSNoteProperty(prop.Name, cert);
                                                    }
                                                }
                                                else
                                                {
                                                    switch (prop.Name)
                                                    {
                                                        case "Path":
                                                            p = new PSNoteProperty(prop.Name, filePath);
                                                            break;
                                                        default:
                                                            p = new PSNoteProperty(prop.Name, str);
                                                            break;
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            p = new PSNoteProperty(prop.Name, prop.Value);
                                        }

                                        if (p != null)
                                        {
                                            result.Properties.Add(p);
                                        }
                                    }
                                }
                            }
                        }

                        WriteObject(result);
                    }
                    else
                    {
                        WriteObject(signature);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, VerbsCommon.Get, ErrorCategory.NotSpecified, null));
            }
        }
    }
}
