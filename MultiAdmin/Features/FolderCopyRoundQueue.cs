﻿using System;
using MultiAdmin.Features.Attributes;

namespace MultiAdmin.Features
{
	[Feature]
	internal class FileCopyRoundQueue : Feature, IEventRoundEnd
	{
		private string[] queue;
		private bool randomizeQueue;
		private int queueIndex;

		public FileCopyRoundQueue(Server server) : base(server)
		{
		}

		public bool HasValidQueue => queue != null && queue.Length > 0;

		public void OnRoundEnd()
		{
			if (!HasValidQueue) return;

			queueIndex = randomizeQueue ? GetNextRandomIndex() : LoopingLimitIndex(queueIndex + 1);

			string copyFrom = queue[queueIndex];

			if (string.IsNullOrEmpty(copyFrom)) return;

			Server.CopyFromDir(copyFrom);
		}

		private int LoopingLimitIndex(int index)
		{
			if (!HasValidQueue) return 0;

			if (index < 0)
				return queue.Length - 1;

			if (index >= queue.Length)
				return 0;

			return index;
		}

		private int GetNextRandomIndex()
		{
			if (!HasValidQueue) return 0;

			Random random = new Random();

			int index;
			do
			{
				index = random.Next(0, queue.Length);
			} while (index == queueIndex);

			return index;
		}

		public override void Init()
		{
			queueIndex = 0;
		}

		public override void OnConfigReload()
		{
			queue = Server.ServerConfig.FolderCopyRoundQueue;
			randomizeQueue = Server.ServerConfig.RandomizeFolderCopyRoundQueue;
		}

		public override string GetFeatureDescription()
		{
			return "Copies files from folders in a queue";
		}

		public override string GetFeatureName()
		{
			return "Folder Copy Round Queue";
		}
	}
}