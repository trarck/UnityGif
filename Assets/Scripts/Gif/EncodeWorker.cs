/*
 * Copyright (c) 2015 Thomas Hourdel
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 *    1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would be
 *    appreciated but is not required.
 * 
 *    2. Altered source versions must be plainly marked as such, and must not be
 *    misrepresented as being the original software.
 * 
 *    3. This notice may not be removed or altered from any source
 *    distribution.
 */

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;
using Gif.Encode;
using ThreadPriority = System.Threading.ThreadPriority;

namespace Gif
{
	internal sealed class EncodeWorker
	{
        private readonly object m_LockObj = new object();

        static int workerId = 1;

		Thread m_Thread;
		int m_Id;
        bool m_Runing = false;

        List<GifFrame> m_ProcessFrames;

        internal List<GifFrame> m_Frames;
		internal GifEncoder m_Encoder;
		internal string m_FilePath;
		internal Action<int, string> m_OnFileSaved;
		internal Action<int, float> m_OnFileSaveProgress;
        
		internal EncodeWorker(ThreadPriority priority)
		{
			m_Id = workerId++;
			m_Thread = new Thread(Run);
			m_Thread.Priority = priority;

            m_Frames = new List<GifFrame>();
            m_ProcessFrames = new List<GifFrame>();
        }

        internal void AddFrame(GifFrame frame)
        {
            lock (m_LockObj)
            {
                m_Frames.Add(frame);
            }
        }

        internal void Clear()
        {
            lock (m_LockObj)
            {
                if (m_Frames != null)
                {
                    m_Frames.Clear();
                }
            }

            if (m_Runing && m_Encoder!=null)
            {
                m_Encoder.Finish();
            }

            m_Runing = false;

            m_Thread.Abort();
        }

        internal void Start()
		{
            if (!m_Runing)
            {
                m_Encoder.Start(m_FilePath);
                m_Runing = true;
                m_Thread.Start();
            }
		}

        internal void Stop()
        {
            m_Runing = false;
        }

		void Run()
		{
            while (m_Runing)
            {
                m_ProcessFrames.Clear();

                lock (m_LockObj)
                {
                    if (m_Frames.Count > 0)
                    {
                        m_ProcessFrames.AddRange(m_Frames);
                        m_Frames.Clear();
                    }
                }

                for (int i = 0; i < m_ProcessFrames.Count; i++)
                {
                    GifFrame frame = m_ProcessFrames[i];
                    m_Encoder.AddFrame(frame);
                }

                Thread.Sleep(100);//0.1
            }

            //check have not process frames
            m_ProcessFrames.Clear();

            lock (m_LockObj)
            {
                if (m_Frames.Count > 0)
                {
                    m_ProcessFrames.AddRange(m_Frames);
                    m_Frames.Clear();
                }
            }

            for (int i = 0; i < m_ProcessFrames.Count; i++)
            {
                GifFrame frame = m_ProcessFrames[i];
                m_Encoder.AddFrame(frame);
            }

            m_Encoder.Finish();

            if (m_OnFileSaved != null)
                m_OnFileSaved(m_Id, m_FilePath);
        }
	}
}
