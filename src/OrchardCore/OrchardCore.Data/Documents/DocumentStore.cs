using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YesSql;

namespace OrchardCore.Data.Documents
{
    /// <summary>
    /// A cacheable and committable document store using the <see cref="ISession"/>.
    /// </summary>
    public class DocumentStore : IDocumentStore
    {
        private readonly ISession _session;

        private readonly Dictionary<Type, object> _loaded = new Dictionary<Type, object>();

        private readonly List<Type> _afterCommitsSuccess = new List<Type>();
        private readonly List<Type> _afterCommitsFailure = new List<Type>();

        private DocumentStoreCommitSuccessDelegate _afterCommitSuccess;
        private DocumentStoreCommitFailureDelegate _afterCommitFailure;

        public DocumentStore(ISession session)
        {
            _session = session;
        }

        /// <inheritdoc />
        public async Task<T> GetMutableAsync<T>(Func<T> factory = null) where T : class, new()
        {
            if (_loaded.TryGetValue(typeof(T), out var loaded))
            {
                return loaded as T;
            }

            var document = await _session.Query<T>().FirstOrDefaultAsync() ?? factory?.Invoke() ?? new T();

            _loaded[typeof(T)] = document;

            return document;
        }

        /// <inheritdoc />
        public async Task<T> GetImmutableAsync<T>(Func<T> factory = null) where T : class, new()
        {
            if (_loaded.TryGetValue(typeof(T), out var loaded))
            {
                _session.Detach(loaded);
            }

            var document = await _session.Query<T>().FirstOrDefaultAsync();

            if (document != null)
            {
                _session.Detach(document);
                return document;
            }

            return factory?.Invoke() ?? new T();
        }

        /// <inheritdoc />
        public Task UpdateAsync<T>(T document, Func<T, Task> updateCache, bool checkConcurrency = false)
        {
            _session.Save(document, checkConcurrency);

            AfterCommitSuccess<T>(() =>
            {
                return updateCache(document);
            });

            AfterCommitFailure<T>(exception =>
            {
                throw new DocumentStoreConcurrencyException(
                    $"The '{typeof(T).Name}' could not be persisted and cached as it has been changed by another process.",
                    exception);
            });

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Cancel() => _session.Cancel();

        /// <inheritdoc />
        public void AfterCommitSuccess<T>(DocumentStoreCommitSuccessDelegate afterCommitSuccess)
        {
            if (!_afterCommitsSuccess.Contains(typeof(T)))
            {
                _afterCommitsSuccess.Add(typeof(T));
                _afterCommitSuccess += afterCommitSuccess;
            }
        }

        /// <inheritdoc />
        public void AfterCommitFailure<T>(DocumentStoreCommitFailureDelegate afterCommitFailure)
        {
            if (!_afterCommitsFailure.Contains(typeof(T)))
            {
                _afterCommitsFailure.Add(typeof(T));
                _afterCommitFailure += afterCommitFailure;
            }
        }

        /// <inheritdoc />
        public async Task CommitAsync()
        {
            if (_session == null)
            {
                return;
            }

            try
            {
                await _session.CommitAsync();

                if (_afterCommitSuccess != null)
                {
                    await _afterCommitSuccess();
                }
            }
            catch (ConcurrencyException exception)
            {
                if (_afterCommitFailure != null)
                {
                    await _afterCommitFailure(exception);
                }
                else
                {
                    throw (exception);
                }
            }
        }
    }
}
