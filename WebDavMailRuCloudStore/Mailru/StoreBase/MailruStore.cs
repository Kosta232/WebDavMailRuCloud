using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MailRuCloudApi;
using MailRuCloudApi.EntryTypes;
using NWebDav.Server.Http;
using NWebDav.Server.Locking;
using NWebDav.Server.Stores;

namespace YaR.WebDavMailRu.CloudStore.Mailru.StoreBase
{
    public sealed class MailruStore : IStore
    {
        public MailruStore(bool isWritable = true, ILockingManager lockingManager = null)
        {
            LockingManager = lockingManager ?? new InMemoryLockingManager();
            IsWritable = isWritable;
        }

        private bool IsWritable { get; }
        private ILockingManager LockingManager { get; }

        public Task<IStoreItem> GetItemAsync(Uri uri, IHttpContext httpContext)
        {
            var path = GetPathFromUri(uri);
            
            try
            {
                var item = Cloud.Instance(httpContext).GetItems(path).Result;

                return null != item
                    ? Task.FromResult(CreateEntry(httpContext, item))
                    : null;
            }
            catch (AggregateException e)
            {
                var we = e.InnerExceptions.OfType<WebException>().FirstOrDefault();
                if (we == null || we.Status != WebExceptionStatus.ProtocolError) throw;
            }
            #if DEBUG
            // ReSharper disable once RedundantCatchClause
            #pragma warning disable 168
            catch (Exception ex)
            {
                throw;
            }
            #pragma warning restore 168
            #endif

            return Task.FromResult<IStoreItem>(null);
        }

        public Task<IStoreCollection> GetCollectionAsync(Uri uri, IHttpContext httpContext)
        {
            var path = GetPathFromUri(uri);
            return Task.FromResult<IStoreCollection>(new MailruStoreCollection(httpContext, LockingManager, new Folder(path), IsWritable));
        }

        private string GetPathFromUri(Uri uri)
        {
            ////can't use uri.LocalPath and so on cause of special signs
            var requestedPath = Regex.Replace(uri.OriginalString, @"^http?://.*?(/|\Z)", string.Empty);
            //TODO: use WebDavPath
            requestedPath = WebDavPath.Root + requestedPath.TrimEnd('/');

            if (string.IsNullOrWhiteSpace(requestedPath)) requestedPath = WebDavPath.Root;

            requestedPath = Uri.UnescapeDataString(requestedPath);

            return requestedPath;
        }
        private IStoreItem CreateEntry(IHttpContext httpContext, IFileOrFolder item)
        {
            //Folder
            if (item is Folder folder)
                return new MailruStoreCollection(httpContext, LockingManager, folder, IsWritable);

            //File
            return new MailruStoreItem(LockingManager, item as File, IsWritable);
        }
    }
}
