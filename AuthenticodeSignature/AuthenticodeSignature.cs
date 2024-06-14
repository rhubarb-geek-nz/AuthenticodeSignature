// Copyright (c) 2024 Roger Brown.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;

namespace RhubarbGeekNz.AuthenticodeSignature
{
    public abstract class AuthenticodeSignaturePSCmdlet : PSCmdlet
    {
        protected byte[] UploadFile(IDictionary<string, string> queryParams, string filePath)
        {
            byte[] response;
            string local = string.Join(Path.DirectorySeparatorChar.ToString(),
                new string[] {
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "rhubarb-geek-nz.signtool",
                    "signtool.config"
                });

            XmlDocument xmlDoc = new XmlDocument();

            xmlDoc.Load(local);

            string endPoint = xmlDoc.SelectSingleNode("/SignTool/Endpoint").InnerText;
            string authorization = xmlDoc.SelectSingleNode("/SignTool/Authorization").InnerText;

            using (WebClient web = new WebClient())
            {
                web.Headers["Authorization"] = authorization;

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

                response = web.UploadFile(url, filePath);
            }

            return response;
        }

        private static readonly string BEGIN_CERTIFICATE = "--BEGIN CERTIFICATE--", END_CERTIFICATE = "--END CERTIFICATE--";

        internal X509Certificate2 GetCertificate(string s)
        {
            X509Certificate2 certificate = null;

            if (s.Contains(BEGIN_CERTIFICATE) && s.Contains(END_CERTIFICATE))
            {
                int i = s.IndexOf(BEGIN_CERTIFICATE);

                if (i > 0)
                {
                    int j = s.LastIndexOf(END_CERTIFICATE);

                    if (j > 0)
                    {
                        i = s.IndexOf('\n', i);

                        if (i > 0)
                        {
                            j = s.LastIndexOf('\n', j);

                            if (j > 0)
                            {
                                string base64 = s.Substring(i + 1, j - i - 1);

                                byte[] bytes = Convert.FromBase64String(base64);

                                certificate = new X509Certificate2(bytes);
                            }
                        }
                    }
                }
            }

            return certificate;
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

                PSObject result = new PSObject();

                result.Properties.Add(new PSNoteProperty("SignerCertificate", Certificate));
                result.Properties.Add(new PSNoteProperty("Status", SignatureStatus.Valid));
                result.Properties.Add(new PSNoteProperty("StatusMessage", "Signature verified."));
                result.Properties.Add(new PSNoteProperty("Path", filePath));

                WriteObject(result);
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
        [Parameter(Mandatory = true)]
        public string FilePath;

        protected override void ProcessRecord()
        {
            try
            {
                IDictionary<string, string> queryParams = new Dictionary<string, string>();

                queryParams.Add("command", "verify");

                string filePath = GetUnresolvedProviderPathFromPSPath(FilePath);

                if (filePath == null)
                {
                    throw new FileNotFoundException(FilePath);
                }

                var response = UploadFile(queryParams, filePath);

                PSObject result = new PSObject();

                using (PowerShell powerShell = PowerShell.Create(RunspaceMode.CurrentRunspace))
                {
                    powerShell.AddCommand("ConvertFrom-Json", true);

                    var output = powerShell.Invoke(new string[] { Encoding.UTF8.GetString(response) });

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
                                                X509Certificate2 cert = GetCertificate(str);

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
                }

                WriteObject(result);
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, VerbsCommon.Get, ErrorCategory.NotSpecified, null));
            }
        }
    }
}
