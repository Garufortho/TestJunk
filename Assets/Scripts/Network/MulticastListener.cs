using UnityEngine;
using System.Collections;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Linq;

public class MulticastListener : MonoBehaviour
{
	static readonly byte[] originalSignal = new byte[] { (int)'D', (int)'E', (int)'A', (int)'D', (int)'B', (int)'E', (int)'E', (int)'F', 0, 0, 0, 0 };
	static byte[] signal = null;
	bool die = false;
	byte[] lastImage;
	[SerializeField] Renderer rend;
	Texture2D tex;
	[SerializeField] bool sendAlpha = true;
	int received;
	bool hasData = false;
	
	void Start()
	{
		tex = new Texture2D(1024, 512, TextureFormat.ARGB32, false);
		if (signal == null)
			signal = Enumerable.Repeat(originalSignal, 3).SelectMany(b => b).ToArray();
		rend.material.mainTexture = tex;
		Listen(bytes=>{ lastImage = bytes; }, "230.0.0.1", 9876);
	}
	
	void Update()
	{
		if (Input.GetKeyDown(KeyCode.H))
			Debug.Log("Received so far: " + received);
		
		if (lastImage != null)
		{
			Debug.Log("Found " + lastImage.Length);
			var image = lastImage;
			lastImage = null;
			/*
			int dataSize = sendAlpha ? 4 : 3;
			
			Color32[] cols = new Color32[image.Length / dataSize];
			for(int i = 0; i < cols.Length; ++i)
			{
				int count = i * dataSize;
				Color32 col = new Color32(0,0,0,1);
				for(int j = 0; j < dataSize; ++j)
				{
					byte val = image[count + j];
					switch(j)
					{
						case 0:
						default:
							col.r = val;
							break;
						case 1:
							col.g = val;
							break;
						case 2:
							col.b = val;
							break;
						case 3:
							col.a = val;
							break;
					}
				}
				cols[i] = col;
			}
			Debug.Log(image.Length + "     " + cols.Length);
			tex.SetPixels32(cols);
			*/
			tex.LoadImage(image);
			//tex.Apply();
		}
	}
	
	void OnApplicationQuit()
	{
		die = true;
	}
	
	void Listen(Action<byte[]> callback, string address, int port)
	{
		Thread t = new Thread(() => ListenWorker(callback, address, port));
		t.Start();
		//ListenWorker(callback, address, port);
	}
	
	void ListenWorker(Action<byte[]> callback, string address, int port)
	{
		using (UdpClient listener = new UdpClient(port))
		{
			IPAddress addr = IPAddress.Parse(address);
			IPEndPoint ep = new IPEndPoint(addr, port);
			listener.JoinMulticastGroup(addr);
			
			//listener.Client.ReceiveBufferSize = 16*4096;
			
			Debug.Log("Listening on " + address + ":" + port.ToString());
			List<byte> lastData = new List<byte>();
			while (!die)
			{
				byte[] data = listener.Receive(ref ep);
				received += data.Length;
				byte[] before;
				byte[] after;
				FindBeforeAndAfterSignal(data, out before, out after);
				bool send = (lastData != null && before == null) || (before != null && before.Length != data.Length);
								
				if (before != null)
					lastData.AddRange(before);
				
				if (send)
				{
					if (lastData != null && lastData.Count > 0)
					{
						callback(lastData.ToArray());
					}
					
					if (after != null)
						lastData = new List<byte>(after);
					else
						lastData = new List<byte>();
				}
			}
		}
	}
	
	void FindBeforeAndAfterSignal(byte[] data, out byte[] before, out byte[] after)
	{
		int foundIndex = -1;
		bool foundSignal = false;
		
		for(int i = 0; i < data.Length; ++i)
		{
			bool found = true;
			for(int j = 0; j < signal.Length; ++j)
			{
				if (data[i + j] != signal[j])
				{
					found = false;
					break;
				}
			}
			if (found)
			{
				foundIndex = i;
				foundSignal = true;
				break;
			}
		}
		if (foundSignal)
		{
			int endBefore = foundIndex;
			int startAfter = foundIndex + signal.Length;
			//Debug.Log("THIS:     " + endBefore + "    " + startAfter);
			
			if (foundIndex == 0)
				before = null;
			else
			{
				before = new byte[endBefore];
				System.Array.Copy(data, before, endBefore);
			}
			
			if (startAfter >= data.Length)
				after = null;
			else
			{
				after = new byte[data.Length - startAfter];
				System.Array.Copy(data, startAfter, after, 0, after.Length);
			}
		}
		else
		{
			before = data;
			after = null;
		}
	}
}
