using Godot;
using System;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;

[Tool]
public partial class CEG_System_v1 : Node
{
	private bool _connectToCascadeur = false;
	private List<Node> _activeAvatars = new List<Node>();
	
	private UdpClient _tlUdpClient;
	private int _currentBakeFrame = 0;
	
	[Export]
	public bool ConnectToCascadeur
	{
		get => _connectToCascadeur;
		set
		{
			_connectToCascadeur = value;
			if (!IsInsideTree()) return;

			if (_connectToCascadeur)
			{
				if (Engine.IsEditorHint() && FileAccess.FileExists("user://ceg_run.lock"))
				{
					DirAccess.RemoveAbsolute("user://ceg_run.lock");
				}
				SendStartCommand();
			}
			else
			{
				SendStopCommand();
			}
		}
	}
	
	public override async void _Ready()
	{
		if (!Engine.IsEditorHint())
		{
			using (var file = FileAccess.Open("user://ceg_run.lock", FileAccess.ModeFlags.Write))
			{
				file?.StoreString("running");
			}
			await ToSignal(GetTree().CreateTimer(0.2f), SceneTreeTimer.SignalName.Timeout);
			ConnectToCascadeur = true;
		}
	}
	
	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint())
		{
			if (ConnectToCascadeur && FileAccess.FileExists("user://ceg_run.lock"))
			{
				ConnectToCascadeur = false; 
				GD.Print("[CEG System] Game launch detected! Editor synchronization has been immediately disabled!");
			}
		}

		if (_connectToCascadeur)
		{
			if (_tlUdpClient != null)
			{
				try
				{
					while (_tlUdpClient.Available > 0)
					{
						System.Net.IPEndPoint ep = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);
						byte[] tlData = _tlUdpClient.Receive(ref ep);
						if (tlData.Length >= 9)
						{
							using (var ms = new System.IO.MemoryStream(tlData))
							using (var br = new System.IO.BinaryReader(ms))
							{
								byte b1 = br.ReadByte(); byte b2 = br.ReadByte(); byte b3 = br.ReadByte(); byte b4 = br.ReadByte(); byte cmd = br.ReadByte();
								if (b1 == 'G' && b2 == 'T' && b3 == 'L' && b4 == 'B' && cmd == 0x03)
								{
									_currentBakeFrame = br.ReadInt32();
								}
							}
						}
					}
				}
				catch { }
			}

			foreach (Node avatar in _activeAvatars)
			{
				if (GodotObject.IsInstanceValid(avatar))
				{
					avatar.Call("ManualProcess", delta, _currentBakeFrame);
				}
			}
		}
	}
	
	public override void _ExitTree()
	{
		if (!Engine.IsEditorHint())
		{
			if (FileAccess.FileExists("user://ceg_run.lock"))
			{
				DirAccess.RemoveAbsolute("user://ceg_run.lock");
			}
		}
		if (_tlUdpClient != null) { _tlUdpClient.Close(); _tlUdpClient = null; }
	}
	
	private void SendStartCommand()
	{
		Node rootNode = Engine.IsEditorHint() ? GetTree().EditedSceneRoot : GetTree().CurrentScene;
		if (rootNode == null) rootNode = this.GetParent();
		
		_activeAvatars.Clear();
		
		List<string> charJsons = new List<string>();
		FindAvatars(rootNode, charJsons, true);

		string fullJson = $"{{\"command\": \"START\", \"characters\": [{string.Join(", ", charJsons)}]}}";
		SendUdpPacket(fullJson);
		GD.Print("[CEG System] Handshake (START) sent to Cascadeur.");

		try
		{
			_tlUdpClient = new UdpClient();
			_tlUdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			_tlUdpClient.Client.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Any, 8989));
			_tlUdpClient.Client.Blocking = false;
		}
		catch (Exception e) { GD.PrintErr($"[CEG System] Timeline UDP Error: {e.Message}"); }
	}

	private void SendStopCommand()
	{
		Node rootNode = Engine.IsEditorHint() ? GetTree().EditedSceneRoot : GetTree().CurrentScene;
		if (rootNode == null) rootNode = this.GetParent();

		List<string> dummy = new List<string>();
		FindAvatars(rootNode, dummy, false);
		
		_activeAvatars.Clear();
		
		SendUdpPacket("{\"command\": \"STOP\"}");
		GD.Print("[CEG System] STOP command sent.");

		if (_tlUdpClient != null) { _tlUdpClient.Close(); _tlUdpClient = null; }
	}

	private void FindAvatars(Node current, List<string> charJsons, bool isStart)
	{
		bool isAvatar = false;
		var script = current.GetScript().As<Script>();
		
		if (script != null && script.ResourcePath.Contains("CEG_Avatar_v1.cs"))
		{
			isAvatar = true;
		}
		else if (current.HasMethod("StartReceiving") && current.HasMethod("ManualProcess"))
		{
			isAvatar = true;
		}

		if (isAvatar)
		{
			if (isStart)
			{
				current.Call("StartReceiving");
				string requestBones = (string)current.Call("GenerateRequestBonesJson");
				int targetPort = (int)current.Get("TargetPort");
				
				charJsons.Add($"{{\"target_port\": {targetPort}, \"request_bones\": {requestBones}}}");
				
				_activeAvatars.Add(current);
			}
			else
			{
				current.Call("StopReceiving");
			}
		}

		foreach (Node child in current.GetChildren())
		{
			FindAvatars(child, charJsons, isStart);
		}
	}

	private void SendUdpPacket(string jsonPayload)
	{
		try
		{
			byte[] bytes = Encoding.UTF8.GetBytes(jsonPayload);
			using (UdpClient sender = new UdpClient())
			{
				sender.Send(bytes, bytes.Length, "127.0.0.1", 8980);
			}
		}
		catch (Exception e) { GD.PrintErr($"[CEG System] UDP Error: {e.Message}"); }
	}
}
