using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeatDodger.Core
{
    [Serializable]
    public struct NoteInfo
    {
        public float time;     
        public int lane;       
        public float duration; 
    }

    [CreateAssetMenu(fileName = "NewNoteMap", menuName = "BeatDodger/NoteMapData")]
    public class NoteMapData : ScriptableObject
    {
        public AudioClip music;
        public float bpm = 120f;
        public List<NoteInfo> notes = new List<NoteInfo>();
    }
}
