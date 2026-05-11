using System.Threading.Tasks;
using BeatDodger.Core;

namespace BeatDodger.Services
{
    public interface IDBService
    {
        /// <summary>
        /// userId와 password로 DB 인증을 수행한다.
        /// 성공 시 UserData, 실패 시 빈 UserData(UserId == null)를 반환한다.
        /// </summary>
        Task<UserData> AuthenticateUserAsync(string userId, string password);

        /// <summary>
        /// 새 사용자를 DB에 등록한다.
        /// 성공 시 true, 아이디 중복 또는 오류 시 false를 반환한다.
        /// </summary>
        Task<bool> RegisterUserAsync(string userId, string password, string userName);
    }
}
