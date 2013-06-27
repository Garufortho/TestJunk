using UnityEngine;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.Linq;

public class MulticastWriter : MonoBehaviour
{
	static readonly byte[] originalSignal = new byte[] { (int)'D', (int)'E', (int)'A', (int)'D', (int)'B', (int)'E', (int)'E', (int)'F', 0, 0, 0, 0 };
	static byte[] signal = null;
	
	object toSendLock = new object();
	Stack<byte[]> toSend = new Stack<byte[]>();
	bool die = false;
	Texture2D tex;
	[SerializeField] bool sendAlpha = true;
	[SerializeField] Renderer rend;
	int sent;
	
	IEnumerator Start()
	{
		if (signal == null)
			signal = Enumerable.Repeat(originalSignal, 3).SelectMany(b => b).ToArray();
		tex = new Texture2D(1024, 512, TextureFormat.ARGB32, false);
		rend.material.mainTexture = tex;
		Color32[] cols = tex.GetPixels32();
		//byte[] send = new byte[cols.Length * 4];
		Write("230.0.0.1", 9876);
		int dataSize = sendAlpha ? 4 : 3;
		
		while(true)
		{
			yield return null;
			if (Input.GetKeyDown(KeyCode.H))
			{
				Debug.Log("Sent so far: " + sent);
			}
			if (Input.GetKeyDown(KeyCode.G))
			{
				for(int i = 0; i < cols.Length; ++i)
				{
					cols[i]= new Color32((byte)Random.Range(0,255), (byte)Random.Range(0,255), (byte)Random.Range(0,255), (byte)255);
				}
				tex.SetPixels32(cols);
				tex.Apply();
				/*
				for(int i = 0; i < cols.Length; ++i)
				{
					for(int j = 0; j < dataSize; ++j)
					{
						var col = cols[i];
						switch(j)
						{
							case 0:
							default:
								send[i*dataSize + j] = col.r;
								break;
							case 1:
								send[i*dataSize + j] = col.g;
								break;
							case 2:
								send[i*dataSize + j] = col.b;
								break;
							case 3:
								send[i*dataSize + j] = col.a;
								break;
						}
					}					
				}
				*/
				AddData(tex.EncodeToPNG());
				//AddData(send);
			}
		}	
	}
	
	void OnApplicationQuit()
	{
		die = true;
	}
	
	void AddData(byte[] data)
	{
		byte[] toAdd = new byte[data.Length + signal.Length];
		System.Array.Copy(data, toAdd, data.Length);
		System.Array.Copy(signal, 0, toAdd, data.Length, signal.Length);
		lock(toSendLock)
		{
			toSend.Push(toAdd);
		}
	}

	void Write(string address, int port)
	{
		Thread t = new Thread(() => WriteWorker(address, port));
		t.Start();
	}
	
	void WriteWorker(string address, int port)
	{
		using (UdpClient writer = new UdpClient())
		{
			var ip = IPAddress.Parse(address);
			writer.JoinMulticastGroup(ip);
			//writer.Client.SendBufferSize = 16*4096;
			var ipEndPoint = new IPEndPoint(ip, port);
			
			Debug.Log("writing to " + address + ":" + port.ToString());
			
			writer.Send(signal, signal.Length, ipEndPoint);
			
			while (!die)
			{
				byte[] next = null;
				lock(toSendLock)
				if (toSend.Count > 0)
				{
					next = toSend.Pop();
				}
				if (next != null)
				{
					Debug.Log("Sending data " + next.Length);
					//writer.BeginSend(next, next.Length, null, null);
					
					for(int i = 0; i < next.Length; i += 1024)
					{
						int amount = i < next.Length - 1024 ? 1024 : next.Length - i;
						byte[] send = new byte[amount];
						System.Array.Copy(next, i, send, 0, amount);
						writer.Send(send, send.Length, ipEndPoint);
						sent += amount;
					}
					
					
					//writer.Send(next, next.Length, ipEndPoint);
				}
				
				Thread.Sleep(50);
			}
		}
	}
}

