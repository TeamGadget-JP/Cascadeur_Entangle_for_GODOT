using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

[Tool]
public partial class CEG_Avatar_v1 : Node
{
	[ExportCategory("Connection Settings")]
	[Export] public int TargetPort = 8981;
	[Export] public string CascadeurPrefix = "";
	[Export] public Skeleton3D TargetSkeleton;
	
	[ExportCategory("Offline Baking (Silent Mode)")]
	[Export] public AnimationPlayer TargetAnimPlayer;
	[Export] public string BakeAnimationName = "BakeResult";
	[Export] public bool EnableBaking = false;
	[Export] public float BakeInterval = 0.033f;

	private Skeleton3D ActualSkeleton => TargetSkeleton ?? Get("TargetSkeleton").As<Skeleton3D>();
	private AnimationPlayer ActualAnimPlayer => TargetAnimPlayer ?? Get("TargetAnimPlayer").As<AnimationPlayer>();
	private int ActualTargetPort => (int)Get("TargetPort");
	private string ActualPrefix => (string)Get("CascadeurPrefix");
	private bool ActualEnableBaking => (bool)Get("EnableBaking");
	private float ActualBakeInterval => (float)Get("BakeInterval");
	private string ActualBakeAnimName 
	{
		get 
		{
			string name = (string)Get("BakeAnimationName");
			return string.IsNullOrEmpty(name) ? "BakeResult" : name;
		}
	}

	private double _bakeTimer = 0.0;
	private int _currentBakeFrame = 0;
	private Dictionary<byte, int> boneMap = new Dictionary<byte, int>();
	
	private UdpClient udpClient;
	private Thread receiveThread;
	private byte[] pendingBytes = null;
	private bool hasNewData = false;
	private bool _isReceiving = false;
	private readonly object lockObj = new object();

	public override void _EnterTree()
	{
		if (!IsInGroup("CEG_Avatars"))
		{
			AddToGroup("CEG_Avatars");
		}
	}

	public string GenerateRequestBonesJson()
	{
		boneMap.Clear();
		Skeleton3D skel = ActualSkeleton;
		if (skel == null) return "{}";

		List<string> entries = new List<string>();
		string pfx = string.IsNullOrEmpty(ActualPrefix) ? "" : ActualPrefix.Trim();
		int boneCount = skel.GetBoneCount();
		byte id = 0;
		
		for (int i = 0; i < boneCount; i++)
		{
			if (id >= 254) break;
			string boneName = skel.GetBoneName(i);
			boneMap[id] = i;

			string requestName = boneName;
			if (requestName.StartsWith("mixamorig_"))
			{
				requestName = requestName.Replace("mixamorig_", "mixamorig:");
			}

			entries.Add($"\"{id}\": \"{pfx}{requestName}\"");
			id++;
		}
		return "{" + string.Join(", ", entries) + "}";
	}

	public void StartReceiving()
	{
		StopReceiving();
		_isReceiving = true;

		try
		{
			udpClient = new UdpClient();
			udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, ActualTargetPort));
			udpClient.Client.Blocking = false;
			receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
			receiveThread.Start();
		}
		catch (Exception e) { GD.PrintErr($"[CEG Avatar] Motion UDP Error: {e.Message}"); }
	}

	public void StopReceiving()
	{
		_isReceiving = false;
		if (receiveThread != null && receiveThread.IsAlive) receiveThread.Join(200);

		if (udpClient != null) { udpClient.Close(); udpClient = null; }
	}

	private void ReceiveLoop()
	{
		IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
		while (_isReceiving && udpClient != null)
		{
			try
			{
				if (udpClient.Available > 0)
				{
					byte[] data = udpClient.Receive(ref ep);
					lock (lockObj) { pendingBytes = data; hasNewData = true; }
				}
				else { Thread.Sleep(1); }
			}
			catch { }
		}
	}

	public void ManualProcess(double delta, int systemBakeFrame)
	{
		Skeleton3D skel = ActualSkeleton;
		if (skel == null) return;

		_currentBakeFrame = systemBakeFrame;

		byte[] dataToProcess = null;
		lock (lockObj)
		{
			if (hasNewData) { dataToProcess = pendingBytes; hasNewData = false; }
		}

		if (dataToProcess != null)
		{
			try
			{
				using (MemoryStream ms = new MemoryStream(dataToProcess))
				using (BinaryReader br = new BinaryReader(ms))
				{
					if (br.ReadByte() == 'C' && br.ReadByte() == 'E' && br.ReadByte() == 'G')
					{
						int numBones = br.ReadByte();
						for (int i = 0; i < numBones; i++)
						{
							byte b_id = br.ReadByte(); byte flags = br.ReadByte();
							bool hasPos = (flags & 2) != 0; bool hasRot = (flags & 1) != 0;

							if (!boneMap.TryGetValue(b_id, out int boneIdx))
							{
								if (hasPos) br.ReadBytes(12);
								if (hasRot) br.ReadBytes(16);
								continue;
							}

							if (hasPos)
							{
								float px = br.ReadSingle(); float py = br.ReadSingle(); float pz = br.ReadSingle();
								skel.SetBonePosePosition(boneIdx, new Vector3(px, py, pz)); // 🌟 変更
							}
							if (hasRot)
							{
								float rx = br.ReadSingle(); float ry = br.ReadSingle(); float rz = br.ReadSingle(); float rw = br.ReadSingle();
								skel.SetBonePoseRotation(boneIdx, new Godot.Quaternion(rx, ry, rz, rw)); // 🌟 変更
							}
						}
					}
				}
			}
			catch { }
		}

		if (ActualEnableBaking && ActualAnimPlayer != null && !string.IsNullOrEmpty(ActualBakeAnimName))
		{
			_bakeTimer += delta;
			if (_bakeTimer >= ActualBakeInterval)
			{
				_bakeTimer = 0.0;
				BakeCurrentPoseToAnimation();
			}
		}
	}

	private void BakeCurrentPoseToAnimation()
	{
		AnimationPlayer animPlayer = ActualAnimPlayer;
		string animName = ActualBakeAnimName;
		Skeleton3D skel = ActualSkeleton;

		if (animPlayer == null || !animPlayer.HasAnimation(animName)) return;

		Animation anim = animPlayer.GetAnimation(animName);
		float currentTime = _currentBakeFrame / 30.0f;

		Node rootForAnim = animPlayer.GetNode(animPlayer.RootNode);
		string skeletonRelativePath = rootForAnim.GetPathTo(skel).ToString();

		for (int i = 0; i < skel.GetBoneCount(); i++)
		{
			string boneName = skel.GetBoneName(i);
			string trackPathStr = $"{skeletonRelativePath}:{boneName}";

			int rotTrackIdx = anim.FindTrack(trackPathStr, Animation.TrackType.Rotation3D);
			if (rotTrackIdx == -1) 
			{
				rotTrackIdx = anim.AddTrack(Animation.TrackType.Rotation3D);
				anim.TrackSetPath(rotTrackIdx, trackPathStr);
			}
			anim.RotationTrackInsertKey(rotTrackIdx, currentTime, skel.GetBonePoseRotation(i));

			int posTrackIdx = anim.FindTrack(trackPathStr, Animation.TrackType.Position3D);
			if (posTrackIdx == -1)
			{
				posTrackIdx = anim.AddTrack(Animation.TrackType.Position3D);
				anim.TrackSetPath(posTrackIdx, trackPathStr);
			}
			anim.PositionTrackInsertKey(posTrackIdx, currentTime, skel.GetBonePosePosition(i));
		}
	}

	public override void _ExitTree()
	{
		StopReceiving();
	}
}
