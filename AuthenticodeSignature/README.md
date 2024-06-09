# AuthenticodeSignature
Tool to sign files using HTTP server

Uses the same arguments as PowerShell's `Set-AuthenticodeSignature`.

Configuration is in an XML file at

| os | path |
| -- | ---- |
| linux, osx | `$HOME/.local/share/rhubarb-geek-nz.signtool/signtool.config` |
| win | `%LOCALAPPDATA%\rhubarb-geek-nz.signtool\signtool.config` |

This contains the endpoint of the service and the credentials.

```
<SignTool>
        <Endpoint>https://localhost:5001/signtool</Endpoint>
        <Authorization>Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ==</Authorization>
</SignTool>
```
