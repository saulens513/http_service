using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Threading;


namespace Server_Socket
{
	class Program
	{
		public static ManualResetEvent thControl = new ManualResetEvent(false);
		//using this for async task
		
		public static void Main(string[] args)
		{
			IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());//getting host data
			
			//setting up endpoint on first ip from the list, using 8080 port, as default
			IPEndPoint ipEP = new IPEndPoint(ipHost.AddressList[0], 8080);
			
			//looking for IPv4
			for (int i = 0; i < ipHost.AddressList.Length; i++) {
				if (ipHost.AddressList[i].ToString().Contains("::")) {
					//Console.WriteLine("IPv6: " + ipHost.AddressList[i]);
				} else {
					//Console.WriteLine("IPv4: " + ipHost.AddressList[i]);
					ipEP = new IPEndPoint(ipHost.AddressList[i], 8080);
					break;
				}
			}
			Console.WriteLine("Listening to: {0}\n", ipEP);
			
			//listener socket, binding it to endpoint and start listening
			Socket sListener = new Socket(ipEP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			sListener.Bind(ipEP);
			sListener.Listen(100);
			while (true) {
				thControl.Reset();//resets thread state
				sListener.BeginAccept(new AsyncCallback(response), sListener);//accepts connection and calls function to handle
				thControl.WaitOne();//stops main thread
			}
		}
		
		//response function, starts when we get incoming connection
		public static void response(IAsyncResult asyncRes)
		{
			try {
				byte[] bReqBuffer = new byte[1024];//byte array to read the request
				byte[] bResHeaderBytes;//byte array for response header
				byte[] bResBytes; //byte array to send response
				string sRequest;//request as string
				string sResponse = "";//response string
				string sLinkRedirect;//link to resource
				int iGetIndex = 0;//index where GET request stops
				string sHttpGet = "";//GET request
				
				thControl.Set();//continues main thread
				
				//new socket which will handle this particular request
				Socket sockListener = (Socket)asyncRes.AsyncState;
				Socket sockHandler = sockListener.EndAccept(asyncRes);
				EndPoint browserIP = sockHandler.RemoteEndPoint;
				
				//receiving request
				sockHandler.Receive(bReqBuffer, bReqBuffer.Length, 0);
				sRequest = Encoding.ASCII.GetString(bReqBuffer);

				//finding the GET part
				if (!sRequest.StartsWith("GET")) {//sometimes browser sends empty requests
					sRequest = "GET / ";
				}
				sHttpGet = sRequest.Substring(5);
				iGetIndex = sHttpGet.IndexOf(" ");
				sHttpGet = sHttpGet.Substring(0, iGetIndex);
				Console.WriteLine("Incoming request from: " + browserIP);
				Console.WriteLine("GET: " + sHttpGet);
				
				//using the GET part i got to find a page or icon (favicon.ico request)
				if (sHttpGet.Equals("favicon.ico")) {
					sLinkRedirect = "icons\\icon.jpg";
				} else {
					//if there is query find page name ignoring the query, page will use query by itself
					if (sHttpGet.Contains("?")) {
						iGetIndex = sHttpGet.IndexOf("?");
						sLinkRedirect = "html\\" + sHttpGet.Substring(0, iGetIndex);
					} else {
						sLinkRedirect = "html\\" + sHttpGet;
					}
					if (!File.Exists(sLinkRedirect)) {
						Console.WriteLine("Page does not exist, redirecting to index page...");
						sLinkRedirect = "html\\index.html";
					}
					Console.WriteLine("Redirecting to: {0}\n", sLinkRedirect);
				}
	
				//opening and reading file
				FileStream fs = new FileStream(sLinkRedirect, FileMode.Open, FileAccess.Read, FileShare.Read);
				BinaryReader bReader = new BinaryReader(fs);
				int iReadBytes;//how much bytes i read in one go
				bResBytes = new byte[fs.Length];
				
				while ((iReadBytes = bReader.Read(bResBytes, 0, bResBytes.Length)) != 0) {
					sResponse = sResponse + Encoding.ASCII.GetString(bResBytes, 0, iReadBytes);
				}
				bReader.Close();
				fs.Close();
				
				//creating a header
				if (sHttpGet.Equals("favicon.ico")) {//if its asking for icon, change to image mimetype
					bResHeaderBytes = createHeader("image/*", sResponse.Length);
				} else {
					bResHeaderBytes = createHeader("text/html", sResponse.Length);
				}
					
				//sending header
				sockHandler.SendTo(bResHeaderBytes, 0, bResHeaderBytes.Length, 0, browserIP);
				
				//sending response (page or icon)
				sockHandler.SendTo(bResBytes, 0, bResBytes.Length, 0, browserIP);
				
				//close socket connection
				sockHandler.Close();
				
			} catch (Exception e) {
				if (e.GetType().ToString().StartsWith("System.Net.Sockets.SocketException")) {
					//after a while, connection times out crashing the system
					//or gets "aborted by the software in host machine"
					//let us know that connection is aborted and continue listening
					Console.WriteLine("ERROR: Connection aborted.");
				} else
					Console.WriteLine("*****\nException: {0}\n*****\n", e);
			}
		}
		
		public static byte[] createHeader(string mimetype, int ContentLenght)
		{
			string sHeader = "";
			sHeader += "HTTP/1.1 200 OK\r\n";
			sHeader += "Content-Type: " + mimetype + "\r\n";
			sHeader += "Content-Length: " + ContentLenght + "\r\n";
			sHeader += "Connection: close\r\n\r\n";
			return Encoding.ASCII.GetBytes(sHeader);
		}
	}
}