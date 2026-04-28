using System;
using UnityEngine;

namespace BeatDodger.Core
{
    /// <summary>
    /// 인게임에서 사용되는 곡 하나의 메타데이터.
    /// Inspector의 SongList에 등록하여 대기실 곡 선택 목록에 표시된다.
    /// </summary>
    [Serializable]
    public struct SongData
    {
        /// <summary>곡 고유 ID. 인게임에서 어떤 리소스를 로드할지 결정한다.</summary>
        public int _Id;

        /// <summary>방 목록과 곡 선택 버튼에 표시되는 곡 이름</summary>
        public string _Name;

        /// <summary>곡 난이도 (1~5 등 기획 정의 범위)</summary>
        public int _Difficulty;

        /// <summary>해시태그 형식의 분위기 설명 (예: "#신남 #빠름 #록")</summary>
        public string _Tags;

        /// <summary>곡 선택 시 대기실에 표시되는 썸네일 이미지. 네트워크로 전송되지 않고 로컬에서 _Id로 조회된다.</summary>
        public Sprite _Thumbnail;
    }
}
