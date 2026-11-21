# Doğrulama sonuçları

## Unity derlemesi

Unity **6000.3.2f1** kullanıldı. Windows Development build başarıyla alındı. Sahne açılışı, aktif UnityTransport, port 7770 ve sunucu kontrollü raket ayarları doğrulandı.

FishNet'in kendi kaynaklarında nullable/eski API uyarıları var; derlemeyi engellemiyor. Unity Cloud projesi pong bağlandı ve Relay etkinleştirildi.

## Yerel iki oyuncu testi

İki bağımsız Windows oyun süreci, gerçek UnityTransport bağlantısı üzerinden test edildi. Test sürücüsü Unity Input System'e sanal klavye olayları gönderdi; oyunun Update/ServerRpc/TargetRpc/SyncVar/NetworkTransform yolları çalıştırıldı. Bu, farklı ağda gerçek bir arkadaşla oynanan deney değildir.

| Kontrol | Sonuç |
| --- | --- |
| Host ve misafir bağlandı, iki raket oluştu | Geçti |
| Host LEFT, misafir RIGHT | Geçti |
| Misafirin G tuşu host skorunu değiştiremedi | İki süreçte geçti |
| C'nin 20 isteğinden 19'u reddedildi | Test aralığındaki sunucu logunda 19 ret |
| X, yalnız bir normal tick kadar hareket ettirdi | Geçti |
| Arka arkaya servis isteği yalnız misafirde TOO FAST gösterdi | İki süreçte geçti |
| Hostun G değişikliği iki tarafta da skoru 9 yaptı | İki süreçte geçti |
| Hostun T değişikliği iki tarafa ulaştı | İki süreçte geçti |
| Misafirin T değişikliği kalıcı olamadı | İki süreçte geçti |
| Hostun Speed değeri 40 yapılınca raket hızlandı | Geçti; sonra 8'e alındı |

Test sürücüsü iki süreçte de `COMPLETE failures=0` yazdı. Özet loglar `yerel-test-kaydi.txt` içinde.

`screenshots/host-g-guest.png` gerçek misafir buildinden alındı: LEFT 9, RIGHT 0, misafir RIGHT, yerel RTT 0 ms. Bu görüntü farklı ağ veya Relay bağlantısı kanıtı değildir.

Sunucu dururken konum değişikliği olmayabileceğinden, raketin yetkili konumu her 10 tickte yeniden gönderilir; misafirin yerel T değişikliği bu güncellemeyle düzeltilir. Client Authoritative kapalı ve Send To Owner açıktır.

## Relay ve teslim durumu

**Otomatik oynama:** Bot A/B iki ayrı süreçte, ardından host/misafir rolleri değiştirilerek iki maç oynadı. Her iki maç da 3–2 bitti ve skorlar iki tarafta eşleşti. Ayrıntılar `OTOMATIK_MACLAR.md` ve `AutoMatches/results.json` içinde. Bu maçlar 127.0.0.1 üzerinde gerçekleşti.

- Multiplayer Services ve FishyUnityTransport paketleri indirildi ve kodla birlikte derlendi.
- Gerçek Relay allocation, katılım kodu ve misafir bağlantısı doğrulandı. Relay üzerinden otomatik maç 3–2 tamamlandı; misafir RTT 67 ms.
- Unity Cloud projesi pong bağlandı; Dashboard Relay kurulumunun tamamlandığını ve etkinleştirildiğini doğruladı. Ayrıntılar RELAY_TESTI.md içinde.
- Farklı ağdaki arkadaşla 3 puanlık maç, rol değişimi, gerçek RTT ve bağlantı kartı BEKLİYOR.
- Dersin Notes 6.1/6.2 dosyaları sağlanmadı; uygulama davranışına uygun açıklamalı bir sürüm hazırlandı.
- Yerel Git commitleri hazırlandı. GitHub adresi verilmediği için push yapılmadı.
- Arşive Library, Temp, kullanıcı ayarları, derlenmiş oyun ve erişim anahtarları eklenmez.

## Testi tekrar çalıştırma

Development build almak için Unity'yi batch modunda `-executeMethod Week06Setup.PrepareAndBuild` ile çalıştırabilirsin. Çıktı varsayılan olarak proje içindeki `Builds/Windows/Pong.exe` olur; `-week06-build-output` ile başka yol verilebilir.

İki süreci aynı gelecekteki Unix saniyesiyle başlat:

```text
Pong.exe -host -smoke-role host -smoke-start UNIX_SANIYESI -logFile host.log
Pong.exe -connect 127.0.0.1:7770 -smoke-role guest -smoke-start UNIX_SANIYESI -logFile guest.log
```

Önceden çalışan bir hostu kapat; port 7770 boş olmalı. Süreyi yaklaşık 15 saniye ilerisi seç. **Önce hostu başlat, LEFT raketi oluşmasını bekle, sonra misafiri başlat.** Aynı anda açılırlarsa misafir ilk bağlanan olup LEFT tarafını alabilir; bu test sürücüsünün host=LEFT varsayımını bozar. İki süreç test sonunda kendiliğinden kapanır. `-smoke-role` verilmeden normal oyunda test sürücüsü oluşturulmaz. Relay testi bu otomasyona dahil değildir.
