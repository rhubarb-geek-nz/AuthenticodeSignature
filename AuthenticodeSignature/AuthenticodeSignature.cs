// Copyright (c) 2024 Roger Brown.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Xml;

namespace RhubarbGeekNz.AuthenticodeSignature
{
    [Cmdlet(VerbsCommon.Set, "AuthenticodeSignature")]
    public class SetAuthenticodeSignature : PSCmdlet
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

                    IDictionary<string, string> queryParams = new Dictionary<string, string>();

                    queryParams.Add("command", "sign");
                    queryParams.Add("t", TimestampServer);
                    queryParams.Add("fd", HashAlgorithm);
                    queryParams.Add("sha1", Certificate.Thumbprint);

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

                    string filePath = GetUnresolvedProviderPathFromPSPath(FilePath);

                    if (filePath == null)
                    {
                        throw new FileNotFoundException(FilePath);
                    }

                    var response = web.UploadFile(url, filePath);

                    File.WriteAllBytes(filePath, response);

                    PSObject result = new PSObject();

                    result.Properties.Add(new PSNoteProperty("SignerCertificate ",Certificate.Thumbprint));
                    result.Properties.Add(new PSNoteProperty("Status", SignatureStatus.Valid));
                    result.Properties.Add(new PSNoteProperty("StatusMessage ", "Signature verified."));
                    result.Properties.Add(new PSNoteProperty("Path ", filePath));

                    WriteObject(result);
                }
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "Set", ErrorCategory.NotSpecified, null));
            }
        }
    }
}
