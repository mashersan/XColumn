using System.Windows;
using GongSolutions.Wpf.DragDrop;
using DragDropEffects = System.Windows.DragDropEffects;

namespace XColumn
{
    /// <summary>
    /// カラムのドラッグ＆ドロップ時の挙動を制御するハンドラー。
    /// 標準の挙動（DefaultDropHandler）を継承し、挿入ガイドの表示のみ強制的に有効化します。
    /// </summary>
    public class ColumnDropHandler : DefaultDropHandler
    {
        public override void DragOver(IDropInfo dropInfo)
        {
            // 標準の判定ロジックを実行
            base.DragOver(dropInfo);

            // ドロップ可能(Move)な場合、視覚効果を「挿入線(Insert)」に強制設定
            if (dropInfo.Effects == DragDropEffects.Move || dropInfo.Effects == DragDropEffects.Copy)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
            }
        }
    }
}