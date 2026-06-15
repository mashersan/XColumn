using GongSolutions.Wpf.DragDrop;
using DragDropEffects = System.Windows.DragDropEffects;

namespace XColumn.Helpers
{
    /// <summary>
    /// カラムのドラッグ＆ドロップ時の挙動を制御するハンドラー。
    /// GongSolutions.WPF.DragDrop の標準ハンドラ（DefaultDropHandler）を継承し、
    /// ドロップ位置のビジュアル（アドナー）を常に「挿入線(Insert)」へ強制することで、
    /// カラムの並べ替え操作で挿入位置が分かりやすくなるようにします。
    /// </summary>
    public class ColumnDropHandler : DefaultDropHandler
    {
        /// <summary>
        /// ドラッグがターゲット上を通過する際の処理。
        /// 標準の判定を実行したうえで、ドロップ可能な場合はアドナーを挿入線に上書きします。
        /// </summary>
        /// <param name="dropInfo">現在のドラッグ＆ドロップ状態を表す情報。</param>
        public override void DragOver(IDropInfo dropInfo)
        {
            // まず標準の判定ロジック（Effects の決定など）を実行する
            base.DragOver(dropInfo);

            // ドロップ可能(Move/Copy)な場合のみ、視覚効果を「挿入線(Insert)」に強制する。
            // 標準では枠線(Highlight)になる場合があるため、並べ替えUIとして分かりやすい挿入線に統一する。
            if (dropInfo.Effects == DragDropEffects.Move || dropInfo.Effects == DragDropEffects.Copy)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
            }
        }
    }
}