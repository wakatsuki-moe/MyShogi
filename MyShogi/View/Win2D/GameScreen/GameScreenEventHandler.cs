﻿using System.Drawing;
using ShogiCore = MyShogi.Model.Shogi.Core;
using MyShogi.App;
using MyShogi.Model.Shogi.Core;
using System;

namespace MyShogi.View.Win2D
{
    /// <summary>
    /// GameScreenに関するイベントハンドラ
    /// マウスがクリックされた時の処理など
    /// </summary>
    public partial class GameScreen
    {
        /// <summary>
        /// Formのリサイズに応じて棋譜コントロールの移動などを行う。
        /// </summary>
        public void ResizeKifuControl()
        {
            var kifu = ViewModel.kifuControl;

            var point = new Point(229, 600);
            kifu.Location = Affine(point);
            var size = new Size(265, 423);
            kifu.Size = AffineScale(size);

            // kifuControl内の文字サイズも変更しないといけない。
            // あとで考える。

            // 駒台が縦長のモードのときは、このコントロールは非表示にする。
            // (何か別の方法で描画する)
            kifu.Visible = TheApp.app.config.KomadaiImageVersion == 1;
        }

        /// <summary>
        /// 描画できる領域が与えられるので、ここにうまく収まるようにaffine行列を設定する。
        /// </summary>
        /// <param name="screenSize"></param>
        public void FitToClientSize(Rectangle screenRect)
        {
            // 縦(h)を基準に横方向のクリップ位置を決める
            // 1920 * 1080が望まれる比率
            int w2 = screenRect.Height * 1920 / 1080;

            // ちらつかずにウインドウのaspect ratioを保つのは.NET Frameworkの範疇では不可能。
            // ClientSizeをResizeイベント中に変更するのは安全ではない。
            // cf. 
            //   https://qiita.com/yu_ka1984/items/b4a3ce9ed7750bd67b86
            // →　あきらめる

#if false
            // このコード、有効にするとハングする。
            double ratio = (double)h / w;
            if (ratio < 0.563)
            {
                w = (int)(h / 0.563);
                ClientSize = new Size(w, h + menu_height);
            }
            else if (ratio > 0.726)
            {
                w = (int)(h / 0.726);
                ClientSize = new Size(w, h + menu_height);
            }
#endif

            var scale = (double)screenRect.Height / board_img_size.Height;
            ViewModel.AffineMatrix.SetMatrix(scale, scale, (screenRect.Width - w2) / 2, screenRect.Top);

            set_komadai(screenRect);
        }

        /// <summary>
        ///  縦長の画面なら駒台を縦長にする。
        /// </summary>
        private void set_komadai(Rectangle screenRect)
        {
            double ratio = (double)screenRect.Width / screenRect.Height;
            //Console.WriteLine(ratio);

            TheApp.app.config.KomadaiImageVersion = (ratio < 1.36) ? 2 : 1;
        }

        /// <summary>
        /// sqの描画する場所を得る。
        /// Config.BoardReverseも反映されている。
        /// </summary>
        /// <param name="sq"></param>
        /// <returns></returns>
        private Point PieceLocation(SquareHand sq)
        {
            var reverse = TheApp.app.config.BoardReverse;
            var color = sq.PieceColor();
            Point dest;

            if (color == ShogiCore.Color.NB)
            {
                // 盤面の升
                Square sq2 = reverse ? ((Square)sq).Inv() : (Square)sq;
                int f = 8 - (int)sq2.ToFile();
                int r = (int)sq2.ToRank();

                dest = new Point(board_location.X + piece_img_size.Width * f, board_location.Y + piece_img_size.Height * r);
            }
            else
            {
                if (reverse)
                    color = color.Not();

                var v = (TheApp.app.config.KomadaiImageVersion == 1) ? 0 : 1;

                var pc = sq.ToPiece();

                if (color == ShogiCore.Color.BLACK)
                    // Point型同士の加算は定義されていないので、第二項をSize型にcastしている。
                    dest = hand_table_pos[v, (int)color] + (Size)hand_piece_pos[v, (int)pc - 1];
                else
                    // 180度回転させた位置を求める
                    // 後手も駒の枚数は右肩に描画するのでそれに合わせて左右のmarginを調整する。
                    dest = new Point(
                        hand_table_pos[v, (int)color].X + (hand_table_size[v].Width - hand_piece_pos[v, (int)pc - 1].X - piece_img_size.Width - 10),
                        hand_table_pos[v, (int)color].Y + (hand_table_size[v].Height - hand_piece_pos[v, (int)pc - 1].Y - piece_img_size.Height + 0)
                    );
            }

            return dest;
        }

        /// <summary>
        /// 画面が汚れているかどうかのフラグ。
        /// これを定期的に監視して、trueになっていれば、親からOnDraw()を呼び出してもらうものとする。
        /// </summary>
        public bool Dirty
        {
            get { return ViewModel.dirty; }
        }

        /// <summary>
        /// 盤面がクリックされたときに呼び出されるハンドラ
        /// </summary>
        /// <param name="p"></param>
        public void OnClick(Point p)
        {
            /// 座標系を、affine変換(逆変換)して、盤面座標系(0,0)-(board_img_width,board_image_height)にする。
            p = ViewModel.AffineMatrix.InverseAffine(p);

            // 盤面(手駒を含む)のどこの升がクリックされたのかを調べる
            SquareHand sq = BoardAxisToSquare(p);

            if (sq != SquareHand.NB)
            {
                if (ViewModel.viewState.picked_from != sq)
                {
                    // この駒をユーザーが掴んで動かそうとしていることを示す
                    ViewModel.viewState.picked_from = sq;

                    // この値が変わったことで画面の状態が変わるので、次回、OnDraw()が呼び出されなくてはならない。
                    ViewModel.dirty = true;
                }
            }

            // デバッグ用にクリックされた升の名前を出力する。
            //Console.WriteLine(sq.Pretty());
        }

        /// <summary>
        /// 盤面がドラッグされたときに呼び出されるハンドラ
        /// </summary>
        /// <param name="p1">ドラッグ開始点</param>
        /// <param name="p2">ドラッグ終了点</param>
        public void OnDrag(Point p1, Point p2)
        {
            /// 座標系を、affine変換(逆変換)して、盤面座標系(0,0)-(board_img_width,board_image_height)にする。
            p1 = ViewModel.AffineMatrix.InverseAffine(p1);
            p2 = ViewModel.AffineMatrix.InverseAffine(p2);

            // 盤面(手駒を含む)のどこの升からどこの升にドラッグされたのかを調べる
            SquareHand sq1 = BoardAxisToSquare(p1);
            SquareHand sq2 = BoardAxisToSquare(p2);

            // 同じ升がクリックされていれば、これはOnClick()として処理してやる。
            if (sq1 == sq2 && sq1 != SquareHand.NB)
            {
                OnClick(p1);
                return;
            }

            // デバッグ用にドラッグされた升の名前を出力する。
            //Console.WriteLine(sq1.Pretty() + '→' + sq2.Pretty());
        }

        /// <summary>
        /// 盤面座標系から、それを表現するSquareHand型に変換する。
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        SquareHand BoardAxisToSquare(Point p)
        {
            var config = TheApp.app.config;

            // 盤上の升かどうかの判定
            var board_rect = new Rectangle(board_location.X, board_location.Y, piece_img_size.Width * 9, piece_img_size.Height * 9);
            if (board_rect.Contains(p))
            {
                // 筋と段
                var f = (File)(8 - (p.X - board_location.X) / piece_img_size.Width);
                var r = (Rank)((p.Y - board_location.Y) / piece_img_size.Height);
                if (File.ZERO <= f && f < File.NB && Rank.ZERO <= r && r < Rank.NB)
                {
                    var sq = Util.MakeSquare(f, r);
                    if (config.BoardReverse)
                        sq = sq.Inv();

                    return (SquareHand)sq;
                }
            }
            else
            {
                // 手駒かどうかの判定
                // 細長駒台があるのでわりと面倒。何も考えずに描画位置で判定する。

                for (var sq = SquareHand.Hand; sq < SquareHand.HandNB; ++sq)
                    if (new Rectangle(PieceLocation(sq), piece_img_size).Contains(p))
                        return sq;
            }

            // not found
            return SquareHand.NB;
        }

    }
}
