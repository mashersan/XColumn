using CommunityToolkit.Mvvm.ComponentModel;

namespace XColumn.ViewModels
{
    /// <summary>
    /// すべてのViewModelの基底クラス。
    /// 現時点では ObservableObject の薄いラッパーですが、
    /// 今後 IsBusy などの共通プロパティを集約する場所として使います。
    /// CommunityToolkit.Mvvm のソースジェネレータを利用するため partial 宣言です。
    /// </summary>
    public abstract partial class ViewModelBase : ObservableObject
    {
        // 共通プロパティの追加例（必要になったら有効化する）:
        // [ObservableProperty] private bool isBusy;
    }
}