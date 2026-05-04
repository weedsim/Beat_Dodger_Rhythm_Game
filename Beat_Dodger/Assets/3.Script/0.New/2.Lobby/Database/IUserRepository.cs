using System.Threading.Tasks;
using BeatDodger.Core;

namespace BeatDodger.Database
{
    /// <summary>
    /// 사용자 데이터 저장소 추상화 인터페이스.
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// DB에서 사용자 데이터를 비동기적으로 로드한다.
        /// </summary>
        /// <param name="userId">조회할 사용자 ID</param>
        /// <returns>로드된 UserData 구조체. 존재하지 않으면 기본값(빈 _UserId).</returns>
        Task<UserData> LoadUserDataAsync(string userId);

        /// <summary>
        /// 사용자 데이터를 DB에 비동기적으로 저장한다.
        /// </summary>
        /// <param name="data">저장할 데이터</param>
        /// <returns>저장 성공 여부</returns>
        Task<bool> SaveUserDataAsync(UserData data);
    }
}
