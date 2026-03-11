using System.Collections.Generic;
using Unity.VisualScripting;

public class MatchMachine
{
    private readonly BoardService _boardService;
    private List<Point> _squarePositions = new List<Point>(); // Точки, где были квадраты
    private readonly Point[] directions =
    {
        Point.up, Point.right, Point.down, Point.left
    };

    public MatchMachine(BoardService boardService)
    {
        _boardService = boardService;
    }

    public List<Point> GetMatchedPoints(Point point, bool main)
    {
        var connectedPoints = new List<Point>();
        var cellTypeAtPoint = _boardService.GetCellTypeAtPoint(point);

        // Если это самолетик - не ищем для него совпадения
        if (cellTypeAtPoint == CellData.CellType.Plane)
            return connectedPoints;

        CheckForDirectionMatch(ref connectedPoints, point, cellTypeAtPoint);
        CheckForMiddleOfMatch(ref connectedPoints, point, cellTypeAtPoint);
        CheckForSquareMatch(ref connectedPoints, point, cellTypeAtPoint); // Проверяем квадраты

        if (main)
        {
            for (int i = 0; i < connectedPoints.Count; i++)
            {
                AddPoints(ref connectedPoints, GetMatchedPoints(connectedPoints[i], false));
            }
        }
        return connectedPoints;
    }

    public void RegisterSquareMatch(Point point, List<Point> squarePoints)
    {
        // Находим правую нижнюю точку квадрата для размещения самолетика
        Point planePoint = FindBottomRightPoint(squarePoints);

        if (!_squarePositions.Contains(planePoint))
        {
            _squarePositions.Add(planePoint);
        }
    }

    private Point FindBottomRightPoint(List<Point> points)
    {
        if (points == null || points.Count == 0) return Point.zero;

        // Ищем точку с максимальным x и максимальным y (правая нижняя)
        Point result = points[0];
        foreach (var point in points)
        {
            if (point.x > result.x || (point.x == result.x && point.y > result.y))
            {
                result = point;
            }
        }
        return result;
    }



    private void CheckForSquareMatch(ref List<Point> connectedPoints, Point point, CellData.CellType cellTypeAtPoint)
    {
        for (int i = 0; i < 4; i++)
        {
            var square = new List<Point>();
            var nextCellIndex = i + 1;
            nextCellIndex = nextCellIndex > 3 ? 0 : nextCellIndex;

            Point[] checkPoints =
            {
                Point.Add(point, directions[i]),
                Point.Add(point, directions[nextCellIndex]),
                Point.Add(point, Point.Add(directions[i], directions[nextCellIndex]))
            };

            // Добавляем исходную точку
            square.Add(point);

            bool allSameType = true;
            foreach (var checkPoint in checkPoints)
            {
                var type = _boardService.GetCellTypeAtPoint(checkPoint);
                if (type != cellTypeAtPoint || type <= 0)
                {
                    allSameType = false;
                    break;
                }
                square.Add(checkPoint);
            }

            // Если нашли квадрат 2x2 из одинаковых кристаллов
            if (allSameType && square.Count == 4)
            {
                // Добавляем все точки квадрата в совпадения
                AddPoints(ref connectedPoints, square);

                // Сообщаем BoardService, что здесь был квадрат
                _boardService.RegisterSquareMatch(point, square);
            }
        }
    }

    //ищет случаи, когда точка находится в середине (не с краю) линии.
    private void CheckForMiddleOfMatch(ref List<Point> connectedPoints, Point point, CellData.CellType cellTypeAtPoint)
    {
        for (int i = 0; i < 2; i++)
        {
            var line = new List<Point>();
            Point[] checkPoints =
            {
                Point.Add(point, directions[i]),
                Point.Add(point, directions[i + 2])

            };

            foreach (var checkPoint in checkPoints)
            {
                if (_boardService.GetCellTypeAtPoint(checkPoint) == cellTypeAtPoint)
                    line.Add(checkPoint);
            }
            if (line.Count > 1)
            {
                AddPoints(ref connectedPoints, line);
            }

        }
    }

    //ищет случаи, когда точка является началом линии совпадений.
    private void CheckForDirectionMatch(ref List<Point> connectedPoints, Point point, CellData.CellType cellTypeAtPoint)
    {
        foreach (var direction in directions)
        {
            var line = new List<Point>();
            for (int i = 1; i <= 2; i++)
            {
                var checkPoint = Point.Add(point, Point.Multiply(direction, i));
                if (_boardService.GetCellTypeAtPoint(checkPoint) == cellTypeAtPoint)
                {
                    line.Add(checkPoint);
                }
            }

            if (line.Count > 1)
            {
                AddPoints(ref connectedPoints, line);
            }
        }
    }

    // собирает все найденные совпадения в один общий список, не допуская дубликатов.
    public static void AddPoints(ref List<Point> points, List<Point> addPoints)
    {
        foreach (var addPoint in addPoints)
        {
            var doAdd = true;
            foreach (var point in points)
            {
                if (point.Equals(addPoint))
                {
                    doAdd = false;
                    break;
                }
            }
            if (doAdd)
            {
                points.Add(addPoint);
            }
        }
    }

    // Добавьте новое поле в класс MatchMachine:
    private readonly List<Point> _detectedSquares = new List<Point>();

    // Добавьте новый метод для проверки квадрата и создания самолетика:
    public bool TryCreatePlaneFromSquare(Point point, out Point squareCenter)
    {
        squareCenter = Point.zero;

        for (int i = 0; i < 4; i++)
        {
            var nextCellIndex = i + 1;
            nextCellIndex = nextCellIndex > 3 ? 0 : nextCellIndex;

            Point[] squarePoints =
            {
            point,
            Point.Add(point, directions[i]),
            Point.Add(point, directions[nextCellIndex]),
            Point.Add(point, Point.Add(directions[i], directions[nextCellIndex]))
        };

            // Проверяем, что все точки валидны
            bool allValid = true;
            foreach (var p in squarePoints)
            {
                if (_boardService.GetCellTypeAtPoint(p) <= 0)
                {
                    allValid = false;
                    break;
                }
            }

            if (!allValid) continue;

            // Проверяем, что все ячейки одного типа и не являются самолетиками
            var firstType = _boardService.GetCellTypeAtPoint(squarePoints[0]);
            if (firstType == CellData.CellType.Plane) continue;

            bool allSame = true;
            foreach (var p in squarePoints)
            {
                if (_boardService.GetCellTypeAtPoint(p) != firstType)
                {
                    allSame = false;
                    break;
                }
            }

            if (allSame)
            {
                // Проверяем, не был ли этот квадрат уже обнаружен
                bool alreadyDetected = false;
                foreach (var detectedPoint in _detectedSquares)
                {
                    if (detectedPoint.Equals(squarePoints[3]))
                    {
                        alreadyDetected = true;
                        break;
                    }
                }

                if (!alreadyDetected)
                {
                    squareCenter = squarePoints[3];
                    _detectedSquares.Add(squareCenter);
                    return true;
                }
            }
        }

        return false;
    }

    // Очистка списка обнаруженных квадратов (вызывать при необходимости)
    public void ClearDetectedSquares()
    {
        _detectedSquares.Clear();
    }
}