using Sitecore.ContentSearch;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.SecurityModel;
using System;
using System.Linq;
using System.Threading;
using Sitecore.ContentSearch;

namespace Sitecore.Support.ContentSearch
{
    public class ExtendedSitecoreItemCrawler : SitecoreItemCrawler
    {
        public ExtendedSitecoreItemCrawler()
        {
        }

        public ExtendedSitecoreItemCrawler(IIndexOperations indexOperations)
            : base(indexOperations)
        {
        }

        public override void Update(IProviderUpdateContext context, IIndexableUniqueId indexableUniqueId, IndexingOptions indexingOptions = IndexingOptions.Default)
        {
            this.Update(context, indexableUniqueId, null, indexingOptions);
        }

        public override void Update(IProviderUpdateContext context, IIndexableUniqueId indexableUniqueId, IndexEntryOperationContext operationContext, IndexingOptions indexingOptions = IndexingOptions.Default)
        {
            Assert.ArgumentNotNull(indexableUniqueId, "indexableUniqueId");
            IProviderUpdateContextEx providerUpdateContextEx = context as IProviderUpdateContextEx;
            if (base.ShouldStartIndexing(indexingOptions))
            {
                Assert.IsNotNull(base.DocumentOptions, "DocumentOptions");
                if (!this.IsExcludedFromIndex(indexableUniqueId, operationContext, true))
                {
                    if (operationContext != null)
                    {
                        if (operationContext.NeedUpdateChildren)
                        {
                            Item item = Sitecore.Data.Database.GetItem(indexableUniqueId as SitecoreItemUniqueId);
                            if (item != null)
                            {
                                if (operationContext.OldParentId != Guid.Empty && this.IsRootOrDescendant(new ID(operationContext.OldParentId)) && !this.IsAncestorOf(item))
                                {
                                    this.Delete(context, indexableUniqueId, IndexingOptions.Default);
                                    return;
                                }
                                this.UpdateHierarchicalRecursive(context, item, CancellationToken.None);
                                return;
                            }
                        }
                        if (operationContext.NeedUpdatePreviousVersion)
                        {
                            Item item2 = Sitecore.Data.Database.GetItem(indexableUniqueId as SitecoreItemUniqueId);
                            if (item2 != null)
                            {
                                this.UpdatePreviousVersion(item2, context);
                            }
                        }
                    }
                    SitecoreIndexableItem indexableAndCheckDeletes = this.GetIndexableAndCheckDeletes(indexableUniqueId);
                    if (indexableAndCheckDeletes == null)
                    {
                        if (this.GroupShouldBeDeleted(indexableUniqueId.GroupId))
                        {
                            this.Delete(context, indexableUniqueId.GroupId, IndexingOptions.Default);
                            return;
                        }
                        this.Delete(context, indexableUniqueId, IndexingOptions.Default);
                        return;
                    }
                    else
                    {
                        this.DoUpdate(context, indexableAndCheckDeletes, operationContext);
                    }
                }
            }
        }

        private bool IsRootOrDescendant(ID id)
        {
            if (base.RootItem.ID == id)
            {
                return true;
            }
            Database database = Sitecore.Data.Database.GetDatabase(base.Database);
            Item item;
            using (new SecurityDisabler())
            {
                item = database.GetItem(id);
            }
            return item != null && base.IsAncestorOf(item);
        }

        private void UpdatePreviousVersion(Item item, IProviderUpdateContext context)
        {
           Sitecore.Data.Version[] array;
            using (new WriteCachesDisabler())
            {
                array = (item.Versions.GetVersionNumbers() ?? new Sitecore.Data.Version[0]);
            }
            int num = array.ToList<Sitecore.Data.Version>().FindIndex((Sitecore.Data.Version version) => version.Number == item.Version.Number);
            if (num >= 1)
            {
                Sitecore.Data.Version previousVersion = array[num - 1];
                Sitecore.Data.Version version2 = array.FirstOrDefault((Sitecore.Data.Version version1) => version1 == previousVersion);
                ItemUri itemUri = new ItemUri(item.ID, item.Language, version2, item.Database.Name);
                SitecoreIndexableItem sitecoreIndexableItem = Sitecore.Data.Database.GetItem(itemUri);
                if (sitecoreIndexableItem != null)
                {
                    IIndexableBuiltinFields indexableBuiltinFields = sitecoreIndexableItem;
                    indexableBuiltinFields.IsLatestVersion = false;
                    sitecoreIndexableItem.IndexFieldStorageValueFormatter = context.Index.Configuration.IndexFieldStorageValueFormatter;
                    base.Operations.Update(sitecoreIndexableItem, context, this.index.Configuration);
                }
            }
        }
    }
}
