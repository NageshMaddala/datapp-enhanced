using API.Interfaces;
using AutoMapper;

namespace API.Data
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;

        public UnitOfWork(DataContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public IUserRepository UserRepository => new UserRepository(_context, _mapper);

        public IMessageRepository MessageRepository => new MessageRepository(_context, _mapper);

        public ILikesRepository LikesRepository => new LikesRepository(_context);

        public async Task<bool> Complete()
        {
            // as long there is more than one thing to save, below line will return true
            return await _context.SaveChangesAsync() > 0;
        }

        public bool HasChanges()
        {
            // if anything is changed in entity in memory
            return _context.ChangeTracker.HasChanges();
        }
    }
}