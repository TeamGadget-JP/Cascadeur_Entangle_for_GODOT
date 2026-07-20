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
		if (TargetSkeleton == null) return "{}";

		List<string> entries = new List<string>();
		string pfx = string.IsNullOrEmpty(CascadeurPrefix) ? "" : CascadeurPrefix.Trim();
		int boneCount = TargetSkeleton.GetBoneCount();
		byte id = 0;
		
		for (int i = 0; i < boneCount; i++)
		{
			if (id >= 254) break;
			string boneName = TargetSkeleton.GetBoneName(i);
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
			udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, TargetPort));
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
		if (TargetSkeleton == null) return;

		_currentBakeFrame = systemBakeFrame;

		// ============================================
		//  Real-time motion application (live preview)
		// ============================================
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
								TargetSkeleton.SetBonePosePosition(boneIdx, new Vector3(px, py, pz));
							}
							if (hasRot)
							{
								float rx = br.ReadSingle(); float ry = br.ReadSingle(); float rz = br.ReadSingle(); float rw = br.ReadSingle();
								TargetSkeleton.SetBonePoseRotation(boneIdx, new Godot.Quaternion(rx, ry, rz, rw));
							}
						}
					}
				}
			}
			catch { }
		}

		// ============================================
		//  Background Keyframe Baking (Silent Bake)
		// ============================================
		if (EnableBaking && TargetAnimPlayer != null && !string.IsNullOrEmpty(BakeAnimationName))
		{
			_bakeTimer += delta;
			if (_bakeTimer >= BakeInterval)
			{
				_bakeTimer = 0.0;
				BakeCurrentPoseToAnimation();
			}
		}
	}

	private void BakeCurrentPoseToAnimation()
	{
		if (TargetAnimPlayer == null || !TargetAnimPlayer.HasAnimation(BakeAnimationName)) return;

		Animation anim = TargetAnimPlayer.GetAnimation(BakeAnimationName);
		float currentTime = _currentBakeFrame / 30.0f;

		Node rootForAnim = TargetAnimPlayer.GetNode(TargetAnimPlayer.RootNode);
		string skeletonRelativePath = rootForAnim.GetPathTo(TargetSkeleton).ToString();

		for (int i = 0; i < TargetSkeleton.GetBoneCount(); i++)
		{
			string boneName = TargetSkeleton.GetBoneName(i);
			string trackPathStr = $"{skeletonRelativePath}:{boneName}";

			int rotTrackIdx = anim.FindTrack(trackPathStr, Animation.TrackType.Rotation3D);
			if (rotTrackIdx == -1) 
			{
				rotTrackIdx = anim.AddTrack(Animation.TrackType.Rotation3D);
				anim.TrackSetPath(rotTrackIdx, trackPathStr);
			}
			anim.RotationTrackInsertKey(rotTrackIdx, currentTime, TargetSkeleton.GetBonePoseRotation(i));

			int posTrackIdx = anim.FindTrack(trackPathStr, Animation.TrackType.Position3D);
			if (posTrackIdx == -1)
			{
				posTrackIdx = anim.AddTrack(Animation.TrackType.Position3D);
				anim.TrackSetPath(posTrackIdx, trackPathStr);
			}
			anim.PositionTrackInsertKey(posTrackIdx, currentTime, TargetSkeleton.GetBonePosePosition(i));
		}
	}

	public override void _ExitTree()
	{
		StopReceiving();
	}
}
