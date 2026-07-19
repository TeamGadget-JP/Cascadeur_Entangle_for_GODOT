using Godot;
using System;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;

[Tool]
public partial class CEG_System_v1 : Node
{
	private bool _connectToCascadeur = false;
	private double _editorCheckTimer = 0.0;
	
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
	}
	
	private void SendStartCommand()
	{
		Node rootNode = Engine.IsEditorHint() ? GetTree().EditedSceneRoot : GetTree().CurrentScene;
		if (rootNode == null) rootNode = this.GetParent();

		List<string> charJsons = new List<string>();
		FindAvatars(rootNode, charJsons, true);

		string fullJson = $"{{\"command\": \"START\", \"characters\": [{string.Join(", ", charJsons)}]}}";
		SendUdpPacket(fullJson);
		GD.Print("[CEG System] Handshake (START) sent to Cascadeur.");
	}

	private void SendStopCommand()
	{
		Node rootNode = Engine.IsEditorHint() ? GetTree().EditedSceneRoot : GetTree().CurrentScene;
		if (rootNode == null) rootNode = this.GetParent();

		List<string> dummy = new List<string>();
		FindAvatars(rootNode, dummy, false);

		SendUdpPacket("{\"command\": \"STOP\"}");
		GD.PrintErr("[CEG System] STOP command sent.");
	}

	private void FindAvatars(Node current, List<string> charJsons, bool isStart)
	{
		if (current is CEG_Avatar_v1 avatar)
		{
			if (isStart)
			{
				avatar.StartReceiving();
				string requestBones = avatar.GenerateRequestBonesJson();
				charJsons.Add($"{{\"target_port\": {avatar.TargetPort}, \"request_bones\": {requestBones}}}");
			}
			else
			{
				avatar.StopReceiving();
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
