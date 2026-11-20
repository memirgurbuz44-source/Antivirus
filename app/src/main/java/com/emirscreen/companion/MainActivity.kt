package com.emirscreen.companion

import android.app.Activity
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.graphics.Color
import android.graphics.Typeface
import android.graphics.drawable.GradientDrawable
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import android.os.BatteryManager
import android.os.Build
import android.os.Bundle
import android.os.PowerManager
import android.os.Process
import android.os.SystemClock
import android.view.ViewGroup
import android.widget.Button
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import java.util.Locale

class MainActivity : Activity() {
    private lateinit var network: TextView
    private lateinit var bandwidth: TextView
    private lateinit var thermal: TextView
    private lateinit var battery: TextView
    private lateinit var cpu: TextView
    private var lastCpu = 0L
    private var lastTime = 0L

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val page = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(22.dp(), 26.dp(), 22.dp(), 24.dp())
            setBackgroundColor(Color.rgb(8, 13, 24))
        }
        val scroll = ScrollView(this)
        scroll.addView(page)
        setContentView(scroll)

        page.addView(label("EMIRSCREEN", 28f, Color.WHITE, true))
        page.addView(label("spacedesk performans yardımcısı", 14f, 0xFFA0B1CA.toInt(), false).padBottom(18))

        val hero = panel()
        hero.addView(label("BAĞLANTI DURUMU", 12f, 0xFF7DCFFF.toInt(), true))
        val state = label("Hazır", 22f, Color.WHITE, true).padTop(8)
        hero.addView(state)
        hero.addView(label("Başlat düğmesi yüklü spacedesk Viewer uygulamasını açar.", 13f, 0xFFB4C0D3.toInt(), false).padTop(4))
        val start = Button(this).apply {
            text = "BAŞLAT – SPACEDESK'İ AÇ"
            isAllCaps = false
            textSize = 15f
            setTextColor(0xFF05121E.toInt())
            background = rounded(0xFF63D7FF.toInt(), 14)
            setOnClickListener {
                val launch = packageManager.getLaunchIntentForPackage("ph.spacedesk.beta")
                if (launch != null) startActivity(launch) else {
                    state.text = "spacedesk yüklü değil"
                    try {
                        startActivity(Intent(Intent.ACTION_VIEW, android.net.Uri.parse("market://details?id=ph.spacedesk.beta")))
                    } catch (_: Exception) {
                        startActivity(Intent(Intent.ACTION_VIEW, android.net.Uri.parse("https://play.google.com/store/apps/details?id=ph.spacedesk.beta")))
                    }
                }
            }
        }
        hero.addView(start, LinearLayout.LayoutParams(-1, 54.dp()).apply { topMargin = 16.dp() })
        page.addView(hero)

        page.addView(label("CANLI TANILAMA", 12f, 0xFF7DCFFF.toInt(), true).padTop(20).padBottom(8))
        network = metric(page, "Wi-Fi / ağ")
        bandwidth = metric(page, "Android ağ bant genişliği tahmini")
        thermal = metric(page, "Cihaz termal durumu")
        battery = metric(page, "Pil sıcaklığı")
        cpu = metric(page, "EmirScreen CPU kullanımı")

        page.addView(label("TAKILMAYI AZALTMA", 12f, 0xFF7DCFFF.toInt(), true).padTop(20).padBottom(8))
        val tips = panel()
        tips.addView(label(
            "• PC ve tablet aynı yerel ağa bağlı olsun.\n\n" +
            "• Mümkünse 5 GHz Wi-Fi kullan.\n\n" +
            "• PC'yi mümkünse Ethernet kablosuyla yönlendiriciye bağla.\n\n" +
            "• Tablet yönlendiriciye yakın olsun.\n\n" +
            "• spacedesk içindeki Quality/Performance ayarını takılma varsa bir kademe düşürmeyi dene.",
            14f, 0xFFD2DCEB.toInt(), false
        ))
        page.addView(tips)
        page.addView(label(
            "ÖNEMLİ: Android, spacedesk'in kendi FPS motorunu dışarıdan değiştiremez. Bu uygulama tanılama yapar; FPS artışını garanti etmez ve 60 Hz paneli 180 Hz yapamaz.",
            12f, 0xFF91A0B5.toInt(), false
        ).padTop(16))
        updateMetrics()
    }

    override fun onResume() { super.onResume(); window.decorView.postDelayed(refresh, 1200) }
    override fun onPause() { window.decorView.removeCallbacks(refresh); super.onPause() }

    private val refresh = object : Runnable {
        override fun run() { updateMetrics(); window.decorView.postDelayed(this, 2000) }
    }

    private fun updateMetrics() {
        val cm = getSystemService(Context.CONNECTIVITY_SERVICE) as ConnectivityManager
        val caps = cm.activeNetwork?.let { cm.getNetworkCapabilities(it) }
        val wifi = caps?.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) == true
        val ethernet = caps?.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET) == true
        network.text = when { wifi -> "Wi-Fi bağlı"; ethernet -> "Ethernet bağlı"; else -> "Wi-Fi algılanmadı" }
        network.setTextColor(if (wifi || ethernet) 0xFF69E1A0.toInt() else 0xFFFFBE64.toInt())
        bandwidth.text = if (caps != null) "İndirme ~${caps.linkDownstreamBandwidthKbps} Kbit/sn • Yükleme ~${caps.linkUpstreamBandwidthKbps} Kbit/sn" else "Bağlantı yok"

        val pm = getSystemService(Context.POWER_SERVICE) as PowerManager
        thermal.text = if (Build.VERSION.SDK_INT >= 29) when (pm.currentThermalStatus) {
            PowerManager.THERMAL_STATUS_NONE -> "Normal"
            PowerManager.THERMAL_STATUS_LIGHT -> "Hafif ısınma"
            PowerManager.THERMAL_STATUS_MODERATE -> "Orta ısınma"
            PowerManager.THERMAL_STATUS_SEVERE -> "Yüksek ısınma – performans düşebilir"
            PowerManager.THERMAL_STATUS_CRITICAL -> "Kritik sıcaklık"
            PowerManager.THERMAL_STATUS_EMERGENCY -> "Acil sıcaklık seviyesi"
            else -> "Bilinmiyor"
        } else "Bu Android sürümünde desteklenmiyor"

        val b = registerReceiver(null, IntentFilter(Intent.ACTION_BATTERY_CHANGED))
        val temp = b?.getIntExtra(BatteryManager.EXTRA_TEMPERATURE, -1) ?: -1
        battery.text = if (temp > 0) String.format(Locale.getDefault(), "%.1f °C", temp / 10.0) else "Okunamadı"

        val now = SystemClock.elapsedRealtime()
        val c = Process.getElapsedCpuTime()
        cpu.text = if (lastTime > 0 && now > lastTime) {
            String.format(Locale.getDefault(), "%.1f%% (yalnızca EmirScreen)", ((c-lastCpu).toDouble()/(now-lastTime)*100).coerceIn(0.0,100.0))
        } else "Ölçülüyor…"
        lastCpu = c
        lastTime = now
    }

    private fun metric(parent: LinearLayout, title: String): TextView {
        val box = panel()
        box.addView(label(title, 12f, 0xFF96AECC.toInt(), true))
        val value = label("Ölçülüyor…", 15f, Color.WHITE, true).padTop(6)
        box.addView(value)
        parent.addView(box)
        return value
    }
    private fun panel() = LinearLayout(this).apply {
        orientation = LinearLayout.VERTICAL
        setPadding(16.dp(), 15.dp(), 16.dp(), 15.dp())
        background = rounded(0xFF131D2F.toInt(), 18)
        layoutParams = LinearLayout.LayoutParams(-1, -2).apply { bottomMargin = 10.dp() }
    }
    private fun label(s: String, size: Float, color: Int, bold: Boolean) = TextView(this).apply {
        text = s; textSize = size; setTextColor(color)
        if (bold) setTypeface(typeface, Typeface.BOLD)
    }
    private fun rounded(color: Int, radius: Int) = GradientDrawable().apply { setColor(color); cornerRadius = radius.dp.toFloat() }
    private fun Int.dp() = (this * resources.displayMetrics.density).toInt()
    private fun TextView.padTop(v: Int) = apply { setPadding(paddingLeft, v.dp(), paddingRight, paddingBottom) }
    private fun TextView.padBottom(v: Int) = apply { setPadding(paddingLeft, paddingTop, paddingRight, v.dp()) }
}
