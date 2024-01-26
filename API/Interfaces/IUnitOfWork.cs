using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Interfaces
{
    /// <summary>
    /// Unit of work is like a transaction
    /// </summary>
    public interface IUnitOfWork
    {
        IUserRepository UserRepository { get; }
        IMessageRepository MessageRepository { get; }
        ILikesRepository LikesRepository { get; }
        /// <summary>
        /// If complete doesn't work everything will roll back
        /// </summary>
        /// <returns></returns>
        Task<bool> Complete();
        /// <summary>
        /// If anything has changed, tracked by EF
        /// </summary>
        /// <returns></returns>
        bool HasChanges();
    }
}