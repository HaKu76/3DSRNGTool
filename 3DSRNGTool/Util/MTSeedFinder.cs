﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using Pk3DSRNGTool.RNG;

namespace Pk3DSRNGTool
{
    public class MTSeedFinder
    {
        private int minframe1, maxframe1, minframe2, maxframe2, poolsize1, _poolsize1, poolsize2, _poolsize2, gap;
        private int[] IV1, IV2;

        public List<Frame_Seed> seedlist = new List<Frame_Seed>();
        public int PreAdvance = 63;  // Misc consumption 60 + 1 + EC + PID

        #region Threading
        public List<Thread> thrds = new List<Thread>();

        public int Cnt, Max; // Progress
        const int Thread_Number = 8;

        public event EventHandler Update;
        private void UpdateProgress(EventArgs e)
        {
            lock (_locker)
            {
                Cnt++;
                Update?.Invoke(this, e);
            }
        }

        public event EventHandler NewResult;
        private void UpdateTable(EventArgs e)
        {
            NewResult?.Invoke(this, e);
        }

        private object _locker = new object();

        public void Abort()
        {
            if (thrds.Count == 0)
                return;
            foreach (var t in thrds)
                if (t.IsAlive)
                    t.Abort();
        }

        public void Clear()
        {
            seedlist.Clear();
            seedlist = new List<Frame_Seed>();
            thrds.Clear();
            Cnt = 0;
        }
        #endregion

        #region Search1
        public void setFinder(int[] iv1, int min1, int max1, int[] iv2, int min2, int max2)
        {
            IV1 = iv1; minframe1 = min1; maxframe1 = max1;
            poolsize1 = max1 - min1 + 1; _poolsize1 = poolsize1 - 5;
            IV2 = iv2; minframe2 = min2; maxframe2 = max2;
            poolsize2 = max2 - min2 + 1; _poolsize2 = poolsize2 - 5;
            gap = minframe2 - maxframe1 - 1;
        }

        private int[] FindFrame(uint seed, uint[] Pool1, uint[] Pool2)
        {
            var rng = new MersenneTwister_Fast(seed);
            int[] result = new int[2];
            int i;
            rng.Next(PreAdvance + minframe1);
            for (i = 0; i < poolsize1; i++)
                Pool1[i] = rng.Nextuint() >> 27;
            for (i = 0; i < _poolsize1; i++)
            {
                if (IV1[0] == Pool1[i] && IV1[1] == Pool1[i + 1] && IV1[2] == Pool1[i + 2]
                 && IV1[3] == Pool1[i + 3] && IV1[4] == Pool1[i + 4] && IV1[5] == Pool1[i + 5])
                    break;
            }
            if (i == _poolsize1)
                return result;
            result[0] = i + minframe1;
            rng.Next(gap);
            for (i = 0; i < poolsize2; i++)
                Pool2[i] = rng.Nextuint() >> 27;
            for (i = 0; i < _poolsize2; i++)
            {
                if (IV2[0] == Pool2[i] && IV2[1] == Pool2[i + 1] && IV2[2] == Pool2[i + 2]
                 && IV2[3] == Pool2[i + 3] && IV2[4] == Pool2[i + 4] && IV2[5] == Pool2[i + 5])
                    break;
            }
            if (i == _poolsize2)
                return result;
            result[1] = i + minframe2;
            return result;
        }

        private void parseseed(uint seed, int frame1, int frame2)
        {
            var frame = new Frame_Seed() { Seed = seed, Frame1 = frame1, Frame2 = frame2 };
            var rng = new MersenneTwister_Fast(seed);
            rng.Next(PreAdvance + 7 + frame1);
            frame.nature1 = (byte)((rng.Nextuint() * 25ul) >> 32);
            rng.Next(frame2 - frame1 - 1);
            frame.nature2 = (byte)((rng.Nextuint() * 25ul) >> 32);
            lock (_locker)
            {
                seedlist.Add(frame);
                UpdateTable(null);
            }
        }

        private void findseed(uint seedmin, uint seedmax)
        {
            uint[] Pool1 = new uint[poolsize1];
            uint[] Pool2 = new uint[poolsize2];
            int[] frame;
            ushort updatepoint = (ushort)seedmax;
            for (uint i = seedmin; i <= seedmax; i++)
            {
                frame = FindFrame(i, Pool1, Pool2);
                if (frame[1] != 0)
                    parseseed(i, frame[0], frame[1]);
                if ((ushort)i == updatepoint)
                {
                    UpdateProgress(null);
                    if (i == 0xFFFFFFFF) // 0xFFFFFFFF ++ = 0
                        break;
                }
            }
            UpdateProgress(null);
        }

        private void findseed1(object param)
        {
            var seedrange = (uint[])param;
            findseed(seedrange[0], seedrange[1]);
        }
        #endregion

        #region Search2
        private int[] IVmin, IVmax;
        private byte Nature;
        private int minframe, maxframe, poolsize, _poolsize;

        public void setFinder2(int[] iv_min, int[] iv_max, int min, int max, byte nature)
        {
            IVmin = iv_min;
            IVmax = iv_max;
            minframe = min;
            maxframe = max;
            Nature = nature;
            poolsize = max - min + 1;
            _poolsize = poolsize - 5;
        }

        private int FindFrame2(uint seed, uint[] Pool)
        {
            var rng = new MersenneTwister_Fast(seed);
            int i;
            rng.Next(PreAdvance + minframe);
            for (i = 0; i < poolsize; i++)
                Pool[i] = rng.Nextuint() >> 27;
            for (i = 0; i < _poolsize; i++)
            {
                if (IVmin[0] <= Pool[i] && Pool[i] <= IVmax[0] &&
                    IVmin[1] <= Pool[i + 1] && Pool[i + 1] <= IVmax[1] &&
                    IVmin[2] <= Pool[i + 2] && Pool[i + 2] <= IVmax[2] &&
                    IVmin[3] <= Pool[i + 3] && Pool[i + 3] <= IVmax[3] &&
                    IVmin[4] <= Pool[i + 4] && Pool[i + 4] <= IVmax[4] &&
                    IVmin[5] <= Pool[i + 5] && Pool[i + 5] <= IVmax[5])
                    break;
            }
            if (i == _poolsize)
                return 0;
            return i + minframe;
        }

        private void parseseed2(uint seed, int frame)
        {
            var NewFrame = new Frame_Seed() { Seed = seed, Frame1 = frame, };
            var rng = new MersenneTwister_Fast(seed);
            rng.Next(PreAdvance + 7 + frame);
            NewFrame.nature1 = (byte)((rng.Nextuint() * 25ul) >> 32);
            if (Nature != NewFrame.nature1)
                return;
            NewFrame.gender = (byte)((rng.Nextuint() * 252ul) >> 32);
            lock (_locker)
            {
                seedlist.Add(NewFrame);
                seedlist = seedlist.OrderBy(t => t.Seed).ToList();
                UpdateTable(null);
            }
        }

        private void findseed2(uint seedmin, uint seedmax)
        {
            uint[] Pool = new uint[poolsize];
            ushort updatepoint = (ushort)seedmax;
            for (uint i = seedmin; i <= seedmax; i++)
            {
                int frame = FindFrame2(i, Pool);
                if (0 != frame)
                    parseseed2(i, frame);
                if ((ushort)i == updatepoint)
                {
                    UpdateProgress(null);
                    if (i == 0xFFFFFFFF) // (0xFFFFFFFF)++ = 0
                        break;
                }
            }
            UpdateProgress(null);
        }

        private void findseed2(object param)
        {
            var seedrange = (uint[])param;
            findseed2(seedrange[0], seedrange[1]);
        }
        #endregion

        public void Search(uint seedmin, uint seedmax, bool full)
        {
            var threadstart = full ? new ParameterizedThreadStart(findseed1) : new ParameterizedThreadStart(findseed2);
            ulong blocksize = (seedmax - seedmin) / Thread_Number + 1;
            ulong i = seedmin;
            List<object> paramlist = new List<object>();
            for (; i + blocksize <= seedmax; i += blocksize)
                paramlist.Add(new uint[] { (uint)i, (uint)(i + blocksize - 1) });
            paramlist.Add(new uint[] { (uint)i, seedmax });
            for (int j = 0; j < paramlist.Count; j++)
            {
                var t = new Thread(threadstart);
                t.IsBackground = true;
                t.Start(paramlist[j]);
                thrds.Add(t);
            }
            Max = (int)((seedmax - seedmin) >> 16) + 1 + paramlist.Count;
        }
    }
}
