# Week06 Pong

Unity **6000.3.2f1** ile bu klasörü aç ve `Assets/Scenes/Pong.unity` sahnesini çalıştır.

**[Türkçe ödev açıklaması, cevaplar ve teslim adımları](Week06/README.md)**

**[Yapılan kontroller ve kalan işler](Week06/TEST_SONUCLARI.md)**

Başlangıç: Week05_Pong_Host.unitypackage. Mevcut sahne, prefablar ve FishNet altyapısı üzerine devam edildi.

Eklenenler: sunucu kontrollü hareket, 30 ms giriş sınırı, C/X/T/G hile deneyleri, oyuncu başına 1 saniye servis sınırı, özel ret mesajı, UnityTransport ve Relay katılım kodu arayüzü.

İki botun kendi kendine oynadığı, roller değiştirilerek tekrarlanan iki maç **3–2** tamamlandı. **Week06 → Build and play automatically (two players)** menüsüyle tekrar oynatabilirsin. Unitypackage importunda eksik Relay paketlerini kuran yardımcı da dahildir.

Relay için kendi Unity Cloud projenin Services üzerinden bağlanması ve Dashboard'da Relay'in etkinleştirilmesi gerekir. İki farklı ağdaki arkadaş deneyi henüz yapılmadı. GitHub depo adresi verilmediği için push yapılmadı.

`Week06NetworkSmoke.cs`, yalnız development/editor ortamında ve `-smoke-role` parametresi verildiğinde çalışan iki süreçli yerel ağ testidir; normal oyunda etkin değildir.
