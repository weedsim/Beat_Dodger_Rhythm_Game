using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeatDodger.Core
{
    [Serializable]
    public struct NoteInfo
    {
        public float time; // 발사 시간 (초)
        public int lane;   // 0 ~ 3 번 레인
    }

    [CreateAssetMenu(fileName = "NewNoteMap", menuName = "BeatDodger/NoteMapData")]
    public class NoteMapData : ScriptableObject
    {
        public AudioClip music;
        public float bpm = 120f;
        public List<NoteInfo> notes = new List<NoteInfo>();
    }
}
