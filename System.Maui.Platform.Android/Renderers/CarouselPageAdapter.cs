using System;
using System.Collections.Specialized;
using System.Linq;
using Android.Content;
#if __ANDROID_29__
using AndroidX.Core.View;
using AndroidX.ViewPager.Widget;
#else
using Android.Support.V4.View;
#endif
using Android.Views;
using System.Maui.Internals;

namespace System.Maui.Platform.Android
{
	internal class CarouselPageAdapter : PagerAdapter, ViewPager.IOnPageChangeListener
	{
		readonly Context _context;
		readonly ViewPager _pager;
		bool _ignoreAndroidSelection;
		CarouselPage _page;

		IElementController ElementController => _page as IElementController;

		public CarouselPageAdapter(ViewPager pager, CarouselPage page, Context context)
		{
			_pager = pager;
			_page = page;
			_context = context;

			page.PagesChanged += OnPagesChanged;
		}

		public override int Count
		{
			get { return _page.Children.Count(); }
		}

		public void OnPageScrolled(int position, float positionOffset, int positionOffsetPixels)
		{
		}

		public void OnPageScrollStateChanged(int state)
		{
		}

		public void OnPageSelected(int position)
		{
			if (_ignoreAndroidSelection)
				return;

			int currentItem = _pager.CurrentItem;
			_page.CurrentPage = currentItem >= 0 && currentItem < ElementController.LogicalChildren.Count ? ElementController.LogicalChildren[currentItem] as ContentPage : null;
		}

		public override void DestroyItem(ViewGroup p0, int p1, Java.Lang.Object p2)
		{
			var holder = (ObjectJavaBox<Tuple<ViewGroup, Page, int>>)p2;
			Page destroyedPage = holder.Instance.Item2;

			IVisualElementRenderer renderer = AppCompat.Platform.GetRenderer(destroyedPage);
			renderer.View.RemoveFromParent();
			holder.Instance.Item1.RemoveFromParent();
		}

		public override int GetItemPosition(Java.Lang.Object item)
		{
			// The int is the current index.
			var holder = (ObjectJavaBox<Tuple<ViewGroup, Page, int>>)item;
			Element parent = holder.Instance.Item2.RealParent;
			if (parent == null)
				return PositionNone;

			// Unfortunately we can't just call CarouselPage.GetIndex, because we need to know
			// if the item has been removed. We could update MultiPage<T> to set removed items' index
			// to -1 to support this if it ever becomes an issue.
			int index = ((CarouselPage)parent).Children.IndexOf(holder.Instance.Item2);
			if (index == -1)
				return PositionNone;

			if (index != holder.Instance.Item3)
			{
				holder.Instance = new Tuple<ViewGroup, Page, int>(holder.Instance.Item1, holder.Instance.Item2, index);
				return index;
			}

			return PositionUnchanged;
		}

		public override Java.Lang.Object InstantiateItem(ViewGroup container, int position)
		{
			ContentPage child = _page.Children.ElementAt(position);
			if (AppCompat.Platform.GetRenderer(child) == null)
				AppCompat.Platform.SetRenderer(child, AppCompat.Platform.CreateRenderer(child, _context));

			IVisualElementRenderer renderer = AppCompat.Platform.GetRenderer(child);
			renderer.View.RemoveFromParent();

			ViewGroup frame = new PageContainer(_context, renderer);

			container.AddView(frame);

			return new ObjectJavaBox<Tuple<ViewGroup, Page, int>>(new Tuple<ViewGroup, Page, int>(frame, child, position));
		}

		public override bool IsViewFromObject(global::Android.Views.View p0, Java.Lang.Object p1)
		{
			var holder = (ObjectJavaBox<Tuple<ViewGroup, Page, int>>)p1;
			ViewGroup frame = holder.Instance.Item1;
			return p0 == frame;
		}

		public void UpdateCurrentItem()
		{
			if (_page.CurrentPage == null)
				throw new InvalidOperationException("CarouselPage has no children.");

			int index = CarouselPage.GetIndex(_page.CurrentPage);
			if (index >= 0 && index < _page.Children.Count)
				_pager.CurrentItem = index;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && _page != null)
			{
				foreach (Element element in ElementController.LogicalChildren)
				{
					var childPage = element as VisualElement;

					if (childPage == null)
						continue;

					IVisualElementRenderer childPageRenderer = AppCompat.Platform.GetRenderer(childPage);
					if (childPageRenderer != null)
					{
						childPageRenderer.View.RemoveFromParent();
						childPageRenderer.Dispose();
						AppCompat.Platform.SetRenderer(childPage, null);
					}
				}
				_page.PagesChanged -= OnPagesChanged;
				_page = null;
			}
			base.Dispose(disposing);
		}

		void OnPagesChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			_ignoreAndroidSelection = true;

			NotifyDataSetChanged();

			_ignoreAndroidSelection = false;

			if (_page.CurrentPage == null)
				return;

			UpdateCurrentItem();
		}
	}
}