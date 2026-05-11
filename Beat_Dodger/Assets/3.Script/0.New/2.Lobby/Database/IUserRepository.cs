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
        /// DB에서 userId와 password로 인증하고 사용자 데이터를 반환한다.
        /// 인증 실패 시 빈 UserData(UserId == null)를 반환한다.
        /// </summary>
        Task<UserData> AuthenticateAsync(string userId, string password);

        /// <summary>
        /// 새 사용자를 DB에 등록한다.
        /// 아이디 중복 시 false를 반환한다.
        /// </summary>
        Task<bool> RegisterUserAsync(string userId, string password, string userName);
    }
}
