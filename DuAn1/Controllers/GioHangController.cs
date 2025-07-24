using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DuAn1.Models;
using System.Linq;
using System.Threading.Tasks;

namespace DuAn1.Controllers
{
    public class GioHangController : Controller
    {
        private readonly Duan1Context _context;

        public GioHangController(Duan1Context context)
        {
            _context = context;
        }

        // Hiển thị giỏ hàng
        public async Task<IActionResult> Index()
        {
            var username = HttpContext.Session.GetString("Username");
            var user = await _context.KhachHangs.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                return RedirectToAction("DangNhap", "DangNhap");
            }

            var cart = await _context.GioHangs
                .Include(g => g.SanPhamGioHangs)
                    .ThenInclude(spg => spg.MaSanPhamNavigation)
                .FirstOrDefaultAsync(g => g.MaKhachHang == user.MaKhachHang);

            return View(cart); // This will pass the GioHang object to the view
        }



        [HttpPost]
        public async Task<IActionResult> AddToCart(string MaSanPham, int SoLuong)
        {
            var username = HttpContext.Session.GetString("Username");
            var user = await _context.KhachHangs.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                return RedirectToAction("DangNhap", "DangNhap");
            }

            var cart = await _context.GioHangs.FirstOrDefaultAsync(g => g.MaKhachHang == user.MaKhachHang);
            if (cart == null)
            {
                cart = new GioHang
                {
                    MaKhachHang = user.MaKhachHang,
                    NgayThem = DateTime.Now,
                    SoLoaiSanPham = 0
                };
                _context.GioHangs.Add(cart);
                await _context.SaveChangesAsync(); // Save the cart first to generate the MaGioHang
            }

            var sanPhamGioHang = await _context.SanPhamGioHangs
                .FirstOrDefaultAsync(sp => sp.MaGioHang == cart.MaGioHang && sp.MaSanPham == MaSanPham);

            if (sanPhamGioHang == null)
            {
                sanPhamGioHang = new SanPhamGioHang
                {
                    MaGioHang = cart.MaGioHang,
                    MaSanPham = MaSanPham,
                    SoLuong = SoLuong
                };
                _context.SanPhamGioHangs.Add(sanPhamGioHang);
            }
            else
            {
                // Only update the quantity, don't touch MaGioHang or MaSanPham
                sanPhamGioHang.SoLuong += SoLuong;
                _context.SanPhamGioHangs.Update(sanPhamGioHang);
            }

            // Update the cart with the new quantity
            cart.SoLoaiSanPham += SoLuong;
            _context.GioHangs.Update(cart);

            await _context.SaveChangesAsync(); // Save all changes
            return RedirectToAction("Index");
        }

        // Xóa sản phẩm khỏi giỏ hàng
        [HttpPost]
        public async Task<IActionResult> RemoveFromCart(string MaSanPham)
        {
            var username = HttpContext.Session.GetString("Username");
            var user = await _context.KhachHangs.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                return RedirectToAction("DangNhap", "DangNhap");
            }

            var cart = await _context.GioHangs.FirstOrDefaultAsync(g => g.MaKhachHang == user.MaKhachHang);
            if (cart != null)
            {
                var sanPhamGioHang = await _context.SanPhamGioHangs
                    .FirstOrDefaultAsync(sp => sp.MaGioHang == cart.MaGioHang && sp.MaSanPham == MaSanPham);

                if (sanPhamGioHang != null)
                {
                    _context.SanPhamGioHangs.Remove(sanPhamGioHang);
                    await _context.SaveChangesAsync();
                }
            }

            return RedirectToAction("Index");
        }



        // Cập nhật số lượng sản phẩm trong giỏ hàng
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(string MaSanPham, int SoLuong)
        {
            var username = HttpContext.Session.GetString("Username");
            var user = await _context.KhachHangs.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                return RedirectToAction("DangNhap", "DangNhap");
            }

            var cart = await _context.GioHangs.FirstOrDefaultAsync(g => g.MaKhachHang == user.MaKhachHang);
            if (cart != null)
            {
                var sanPhamGioHang = await _context.SanPhamGioHangs
                    .FirstOrDefaultAsync(sp => sp.MaGioHang == cart.MaGioHang && sp.MaSanPham == MaSanPham);

                if (sanPhamGioHang != null)
                {
                    // Update only the quantity of the product, not the foreign keys.
                    sanPhamGioHang.SoLuong = SoLuong;
                    _context.SanPhamGioHangs.Update(sanPhamGioHang);
                    await _context.SaveChangesAsync();
                }
            }

            return RedirectToAction("Index");
        }




        [HttpPost]
        public async Task<IActionResult> ThanhToan()
        {
            var username = HttpContext.Session.GetString("Username");
            var user = await _context.KhachHangs.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                return RedirectToAction("DangNhap", "DangNhap");
            }

            var cart = await _context.GioHangs
                .Include(g => g.SanPhamGioHangs)
                    .ThenInclude(spg => spg.MaSanPhamNavigation)
                        .ThenInclude(sp => sp.MaKhuyenMaiNavigation) // Bao gồm mã khuyến mãi
                .FirstOrDefaultAsync(g => g.MaKhachHang == user.MaKhachHang);

            if (cart == null || !cart.SanPhamGioHangs.Any())
            {
                return RedirectToAction("Index");
            }

            var hoaDon = new HoaDon
            {
                MaHoaDon = "HD" + (await _context.HoaDons.CountAsync() + 1),
                NgayTao = DateTime.Now,
                ThanhTien = cart.SanPhamGioHangs.Sum(item =>
                {
                    var donGia = item.MaSanPhamNavigation?.DonGia ?? 0;
                    var phanTramGiam = 0m; // Khởi tạo phần trăm giảm giá

                    var khuyenMai = item.MaSanPhamNavigation?.MaKhuyenMaiNavigation;
                    if (khuyenMai != null && khuyenMai.NgayBatDau <= DateTime.Now && khuyenMai.NgayKetThuc >= DateTime.Now)
                    {
                        phanTramGiam = khuyenMai.PhanTramGiam ?? 0;
                    }

                    // Tính giá sau khi giảm
                    var giaSauGiam = donGia - (donGia * phanTramGiam / 100);
                    return item.SoLuong * giaSauGiam;
                }),
                TrangThai = "Chờ xác nhận",
                MaGioHang = cart.MaGioHang,
                MaKhachHang = cart.MaKhachHang
            };

            _context.HoaDons.Add(hoaDon);
            await _context.SaveChangesAsync();

            foreach (var item in cart.SanPhamGioHangs)
            {
                var donGia = item.MaSanPhamNavigation?.DonGia ?? 0;
                var phanTramGiam = 0m; // Khởi tạo phần trăm giảm giá

                var khuyenMai = item.MaSanPhamNavigation?.MaKhuyenMaiNavigation;
                if (khuyenMai != null && khuyenMai.NgayBatDau <= DateTime.Now && khuyenMai.NgayKetThuc >= DateTime.Now)
                {
                    phanTramGiam = khuyenMai.PhanTramGiam ?? 0;
                }

                // Tính giá sau khi giảm
                var donGiaSauGiam = donGia - (donGia * phanTramGiam / 100);

                var hoaDonChiTiet = new HoaDonChiTiet
                {
                    MaHoaDon = hoaDon.MaHoaDon,
                    MaSanPham = item.MaSanPham,
                    SoLuong = item.SoLuong,
                    DonGia = donGiaSauGiam // Sử dụng giá đã giảm
                };

                _context.HoaDonChiTiets.Add(hoaDonChiTiet);
            }

            // Xóa giỏ hàng sau khi thanh toán
            var carts = await _context.GioHangs
                .FirstOrDefaultAsync(g => g.MaGioHang == hoaDon.MaGioHang);

            if (carts != null)
            {
                var cartItems = await _context.SanPhamGioHangs
                    .Where(spg => spg.MaGioHang == carts.MaGioHang)
                    .ToListAsync();

                _context.SanPhamGioHangs.RemoveRange(cartItems);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("XemHoaDon", new { maHoaDon = hoaDon.MaHoaDon });
        }



        // Xác nhận thanh toán
        [HttpPost]
        public async Task<IActionResult> HuyHoaDon(string MaHoaDon)
        {
            var hoaDon = await _context.HoaDons.FirstOrDefaultAsync(hd => hd.MaHoaDon == MaHoaDon);

            if (hoaDon == null)
            {
                return RedirectToAction("Index"); // If the invoice is not found, redirect to the cart page
            }

            // Update the invoice status to "Paid"
            hoaDon.TrangThai = "Hủy";
            _context.HoaDons.Update(hoaDon);
            await _context.SaveChangesAsync();

            return RedirectToAction("XemHoaDon", new { maHoaDon = hoaDon.MaHoaDon });
        }


        // Action để xem hóa đơn
        public async Task<IActionResult> XemHoaDon(string maHoaDon)
        {
            // Lấy hóa đơn từ cơ sở dữ liệu
            var hoaDon = await _context.HoaDons
                .Include(hd => hd.HoaDonChiTiets)
                    .ThenInclude(hdct => hdct.MaSanPhamNavigation)
                .FirstOrDefaultAsync(hd => hd.MaHoaDon == maHoaDon);

            if (hoaDon == null)
            {
                return RedirectToAction("Index"); // Nếu không tìm thấy hóa đơn, quay lại trang giỏ hàng
            }

            return View(hoaDon); // Trả về trang xem hóa đơn 
        }
    }
}
