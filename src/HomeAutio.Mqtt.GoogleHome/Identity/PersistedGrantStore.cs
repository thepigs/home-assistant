﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace HomeAutio.Mqtt.GoogleHome.Identity
{
    /// <summary>
    /// Persisted grant store.
    /// </summary>
    public class PersistedGrantStore : IPersistedGrantStoreWithExpiration
    {
        private readonly ILogger<PersistedGrantStore> _log;
        private readonly ConcurrentDictionary<string, PersistedGrant> _repository = new ConcurrentDictionary<string, PersistedGrant>();
        private readonly string _file;

        // Explicitly use the default contract resolver to force exact property serialization Base64 keys as they are case sensitive
        private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings { ContractResolver = new DefaultContractResolver() };

        private object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="PersistedGrantStore"/> class.
        /// </summary>
        /// <param name="logger">Logging instance.</param>
        /// <param name="configuration">Conffguration.</param>
        public PersistedGrantStore(ILogger<PersistedGrantStore> logger, IConfiguration configuration)
        {
            _log = logger;
            _file = configuration.GetValue<string>("oauth:tokenStoreFile");
            RestoreFromFile();
        }

        /// <inheritdoc />
        public Task StoreAsync(PersistedGrant grant)
        {
            _repository[grant.Key] = grant;

            WriteToFile();

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<PersistedGrant> GetAsync(string key)
        {
            PersistedGrant token;
            if (_repository.TryGetValue(key, out token))
            {
                return Task.FromResult(token);
            }

            _log.LogWarning($"Failed to find token with key {key}");
            return Task.FromResult<PersistedGrant>(null);
        }

        /// <inheritdoc />
        public Task<IEnumerable<PersistedGrant>> GetAllAsync(string subjectId)
        {
            var query = _repository
                .Where(x => x.Value.SubjectId == subjectId)
                .Select(x => x.Value);

            var items = query.ToArray().AsEnumerable();
            return Task.FromResult(items);
        }

        /// <inheritdoc />
        public Task RemoveAsync(string key)
        {
            if (!_repository.TryRemove(key, out _))
            {
                WriteToFile();
            }
            else
            {
                _log.LogWarning($"Failed to remove token with key {key}");
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task RemoveAllAsync(string subjectId, string clientId)
        {
            var query = _repository
                .Where(x => x.Value.ClientId == clientId && x.Value.SubjectId == subjectId)
                .Select(x => x.Key);

            var keys = query.ToArray();
            foreach (var key in keys)
            {
                if (!_repository.TryRemove(key, out _))
                {
                    _log.LogWarning($"Failed to remove token with key {key}");
                }
            }

            WriteToFile();

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task RemoveAllAsync(string subjectId, string clientId, string type)
        {
            var query = _repository
                .Where(x => x.Value.SubjectId == subjectId && x.Value.ClientId == clientId && x.Value.Type == type)
                .Select(x => x.Key);

            var keys = query.ToArray();
            foreach (var key in keys)
            {
                if (!_repository.TryRemove(key, out _))
                {
                    _log.LogWarning($"Failed to remove token with key {key}");
                }
            }

            WriteToFile();

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task RemoveAllExpiredAsync()
        {
            var query = _repository
                .Where(x => x.Value.Expiration < DateTime.UtcNow)
                .Select(x => x.Key);

            var keys = query.ToArray();
            foreach (var key in keys)
            {
                if (!_repository.TryRemove(key, out _))
                {
                    _log.LogWarning($"Failed to remove token with key {key}");
                }
            }

            WriteToFile();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Initialize current state from file.
        /// </summary>
        private void RestoreFromFile()
        {
            if (File.Exists(_file))
            {
                lock (_lock)
                {
                    var fileContents = File.ReadAllText(_file);
                    var deserializedFileContents = JsonConvert.DeserializeObject<Dictionary<string, PersistedGrant>>(fileContents, _jsonSerializerSettings);

                    _repository.Clear();
                    foreach (var record in deserializedFileContents)
                    {
                        if (!_repository.TryAdd(record.Key, record.Value))
                        {
                            _log.LogWarning($"Failed to restore token with key {record.Key}");
                        }
                    }

                    _log.LogInformation($"Restored tokens from {_file}");
                }
            }
        }

        /// <summary>
        /// Write the current state to file.
        /// </summary>
        private void WriteToFile()
        {
            lock (_lock)
            {
                var contents = JsonConvert.SerializeObject(_repository, _jsonSerializerSettings);
                File.WriteAllText(_file, contents);

                _log.LogInformation($"Wrote tokens to {_file}");
            }
        }
    }
}
