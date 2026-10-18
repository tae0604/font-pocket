using System;
using System.Net;

namespace FontPocket {
    internal static class SecureHttp {
        static SecureHttp() {
            // csc-built executables can inherit the older .NET TLS defaults even
            // on a machine with .NET 4.8. Opt into Windows protocol negotiation
            // before creating either an update or font-download connection.
            AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto",false);
            AppContext.SetSwitch("Switch.System.Net.DontEnableSystemDefaultTlsVersions",false);
            ServicePointManager.SecurityProtocol=SecurityProtocolType.SystemDefault;
        }
        internal static HttpWebRequest Create(string url) {
            return (HttpWebRequest)WebRequest.Create(url);
        }
    }
}
