// 캡처·사진 속 글자 읽기: 기기 안에서 처리하는 구글 ML Kit 한국어 글자 인식 (인터넷 필요 없음, 이미지가 밖으로 나가지 않음)
import 'package:google_mlkit_text_recognition/google_mlkit_text_recognition.dart';

Future<String> ocrImage(String path) async {
  final rec = TextRecognizer(script: TextRecognitionScript.korean);
  try {
    final r = await rec.processImage(InputImage.fromFilePath(path));
    return r.text;
  } finally {
    await rec.close();
  }
}
